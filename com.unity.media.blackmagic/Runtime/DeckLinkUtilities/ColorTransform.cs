using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// Enum values for BMD supported input/output color spaces
    /// </summary>
    public enum BMDColorSpace
    {
        /// <summary>
        /// Uses the default color space coming from the video.
        /// </summary>
        UseDeviceSignal = 0,

        /// <summary>
        /// This display mode uses the ITU-R Recommendation BT.601 standard for encoding or decoding the video signal.
        /// </summary>
        BT601 = (1 << 1),

        /// <summary>
        /// This display mode uses the ITU-R Recommendation BT.709 standard for encoding or decoding the video signal.
        /// </summary>
        BT709 = (1 << 2),

        /// <summary>
        /// This display mode uses the ITU-R Recommendation BT.2020/2100 standard for encoding or decoding the video signal.
        /// </summary>
        BT2020 = (1 << 3)
    }

    /// <summary>
    /// The possible dynamic range standards for ITU-R Recommendation BT.2020/2100 video signals.
    /// </summary>
    public enum BMDTransferFunction
    {
        /// <summary>
        /// Uses the default transfer function coming from the video.
        /// </summary>
        UseDeviceSignal = 0,

        /// <summary>
        /// The default High Dynamic Range.
        /// </summary>
        HDR = 1,

        /// <summary>
        /// The High Dynamic Range Perceptual Quantizer transfer function (SMPTE ST 2084).
        /// </summary>
        PQ = 2,

        /// <summary>
        /// The High Dynamic Range Hybrid Log Gamma transfer function (ITU-R BT.2100-0).
        /// </summary>
        HLG = 3
    }

    internal class ColorTransform
    {
        internal static class Contents
        {
            internal static readonly string VerticalFlipKeyword = "VERTICAL_FLIP";
            internal static readonly string WSConversionKeyword = "WORKING_SPACE_CONVERSION";

            internal static readonly string InShader = "Hidden/BlackmagicVideo/CUConvertInput";
            internal static readonly string OutShader = "Hidden/BlackmagicVideo/CUConvertOutput";
        }

        // Cached strings to avoid gc
        static readonly Dictionary<BMDColorSpace, string> BMDColorSpaceString = ((BMDColorSpace[])Enum.GetValues(typeof(BMDColorSpace)))
            .ToDictionary(x => x, x => x.ToString());

        static readonly Dictionary<int, string> BMDPixelFormatString = ((BMDPixelFormat[])Enum.GetValues(typeof(BMDPixelFormat)))
            .ToDictionary(x => (int)x, x => x.ToString());

        internal struct MaterialCacheKey : IEquatable<MaterialCacheKey>
        {
            internal int pixelFormat;
            internal BMDColorSpace colorSpace;
            internal BMDTransferFunction transferFunction;
            internal bool requiresVerticalFlip;
            internal bool requiresWorkingSpaceConversion;
            internal bool requiresLinearConversion;

            public override bool Equals(object obj)
            {
                if (obj is MaterialCacheKey rhs)
                {
                    return Equals(rhs);
                }
                return false;
            }

            public bool Equals(MaterialCacheKey rhs)
            {
                return rhs.pixelFormat == pixelFormat &&
                    rhs.colorSpace == colorSpace &&
                    rhs.transferFunction == transferFunction &&
                    rhs.requiresVerticalFlip == requiresVerticalFlip &&
                    rhs.requiresWorkingSpaceConversion == requiresWorkingSpaceConversion &&
                    rhs.requiresLinearConversion == requiresLinearConversion;
            }

            public override int GetHashCode() =>
                (
                    pixelFormat,
                    colorSpace,
                    transferFunction,
                    requiresVerticalFlip,
                    requiresWorkingSpaceConversion,
                    requiresLinearConversion
                ).GetHashCode();
        }

        /// <summary>
        /// Get the appropriate material to transform images between unity and an external source.
        /// </summary>
        /// <remarks>
        /// Obtain the Material required to blit the pixels to and from the Unity internal representation,
        /// based on the format. These Materials are cached for fast access. One needs to call @Reset to
        /// empty the cache of materials.
        /// </remarks>
        /// <param name="pixelFormat">The API format value representing the input packing.</param>
        /// <param name="cs">The color space conversion requested</param>
        /// <param name="mappings">The mappings for the current transform direction.</param>
        /// <param name="materials">The cache location.</param>
        /// <returns></returns>
        internal static Material Get(
            MaterialCacheKey key,
            string shaderName,
            Dictionary<MaterialCacheKey, Material> materials)
        {
            Assert.IsTrue(BMDColorSpaceString.Count > 0 && BMDPixelFormatString.Count > 0);

            if (!materials.ContainsKey(key))
            {
                var shader = Shader.Find(shaderName);
                Assert.IsNotNull(shader, $"{shader} is not included in the 'Always Include Shader' list.");
                Assert.IsTrue(shader.isSupported);
                materials[key] = new Material(shader);
                materials[key].hideFlags = HideFlags.HideAndDontSave;

                // Required keywords for branch compilation
                materials[key].EnableKeyword(BMDPixelFormatString[key.pixelFormat]);

                materials[key].EnableKeyword(BMDColorSpaceString[key.colorSpace]);

                if (key.requiresLinearConversion)
                    materials[key].EnableKeyword(BlackmagicUtilities.k_KeywordLinearColorConversion);

                if (key.requiresWorkingSpaceConversion)
                    materials[key].EnableKeyword(Contents.WSConversionKeyword);

                if (key.requiresVerticalFlip)
                    materials[key].EnableKeyword(Contents.VerticalFlipKeyword);
            }
            return materials[key];
        }

#if UNITY_EDITOR
        /// <summary>
        /// Ensure the proper runtime availability of the parametrized shader name.
        /// </summary>
        /// <remarks>
        /// Based on logic exposed here:
        /// https://forum.unity.com/threads/modify-always-included-shaders-with-pre-processor.509479/
        /// </remarks>
        /// <param name="shaderName">The name of the shader to validate.</param>
        internal static void AddAlwaysIncludedShader(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
                return;

            var graphicsSettingsObj = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            var serializedObject = new SerializedObject(graphicsSettingsObj);
            var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");
            var hasShader = false;
            for (int i = 0; i < arrayProp.arraySize; ++i)
            {
                var arrayElem = arrayProp.GetArrayElementAtIndex(i);
                if (shader == arrayElem.objectReferenceValue)
                {
                    hasShader = true;
                    break;
                }
            }

            if (!hasShader)
            {
                var arrayIndex = arrayProp.arraySize;
                arrayProp.InsertArrayElementAtIndex(arrayIndex);
                var arrayElem = arrayProp.GetArrayElementAtIndex(arrayIndex);
                arrayElem.objectReferenceValue = shader;

                serializedObject.ApplyModifiedProperties();

                AssetDatabase.SaveAssets();
            }
        }

#endif

        internal static void AddShadersToSettings()
        {
#if UNITY_EDITOR
            AddAlwaysIncludedShader(Contents.OutShader);
            AddAlwaysIncludedShader(Contents.InShader);
#endif
        }

        internal static string ColorSpaceToString(BMDColorSpace cs)
        {
            switch (cs)
            {
                case BMDColorSpace.BT601: return "BT 601";
                case BMDColorSpace.BT709: return "BT 709";
                case BMDColorSpace.BT2020: return "BT 2020";
                default: return "Not defined";
            }
        }
    }

    internal class InputColorTransform : ColorTransform
    {
        /// <summary>
        /// Runtime dictionaries of loaded materials.
        /// </summary>
        static Dictionary<MaterialCacheKey, Material> InMaterials = new Dictionary<MaterialCacheKey, Material>();

        internal static Material Get(
            BMDPixelFormat inPixelFormat,
            BMDColorSpace cs,
            BMDTransferFunction transferFunction,
            bool workingSpaceConversion
        )
        {
            MaterialCacheKey key;
            key.pixelFormat = (int)inPixelFormat;
            key.colorSpace = cs;
            key.transferFunction = transferFunction;
            key.requiresVerticalFlip = true;
            key.requiresWorkingSpaceConversion = workingSpaceConversion;
            key.requiresLinearConversion = BlackmagicUtilities.RequireLinearColorConversion();
            return Get(key, Contents.InShader, InMaterials);
        }

        internal static Material Get(
            BMDPixelFormat inPixelFormat,
            BMDColorSpace cs,
            BMDTransferFunction transferFunction,
            bool workingSpaceConversion,
            bool requiresLinearConversion
        )
        {
            MaterialCacheKey key;
            key.pixelFormat = (int)inPixelFormat;
            key.colorSpace = cs;
            key.transferFunction = transferFunction;
            key.requiresVerticalFlip = true;
            key.requiresWorkingSpaceConversion = workingSpaceConversion;
            key.requiresLinearConversion = requiresLinearConversion;
            return Get(key, Contents.InShader, InMaterials);
        }

        internal static void Reset()
        {
            foreach (var mat in InMaterials)
            {
                BlackmagicUtilities.Destroy(mat.Value);
            }
            InMaterials.Clear();
        }
    }

    internal class OutputColorTransform : ColorTransform
    {
        /// <summary>
        /// Runtime dictionaries of loaded materials.
        /// </summary>
        static Dictionary<MaterialCacheKey, Material> OutMaterials = new Dictionary<MaterialCacheKey, Material>();

        internal static Material Get(
            BMDPixelFormat pixelFormat,
            BMDColorSpace cs,
            BMDTransferFunction transferFunction,
            bool workingSpaceConversion)
        {
            MaterialCacheKey key;
            key.pixelFormat = (int)pixelFormat;
            key.colorSpace = cs;
            key.transferFunction = transferFunction;
            key.requiresVerticalFlip = true;
            key.requiresWorkingSpaceConversion = workingSpaceConversion;
            key.requiresLinearConversion = BlackmagicUtilities.RequireLinearColorConversion();
            return Get(key, Contents.OutShader, OutMaterials);
        }

        internal static Material Get(
            BMDPixelFormat pixelFormat,
            BMDColorSpace cs,
            BMDTransferFunction transferFunction,
            bool workingSpaceConversion,
            bool requiresLinearConversion)
        {
            MaterialCacheKey key;
            key.pixelFormat = (int)pixelFormat;
            key.colorSpace = cs;
            key.transferFunction = transferFunction;
            key.requiresVerticalFlip = true;
            key.requiresWorkingSpaceConversion = workingSpaceConversion;
            key.requiresLinearConversion = requiresLinearConversion;
            return Get(key, Contents.OutShader, OutMaterials);
        }

        internal static void Reset()
        {
            foreach (var mat in OutMaterials)
            {
                BlackmagicUtilities.Destroy(mat.Value);
            }
            OutMaterials.Clear();
        }
    }
}
