using System;
using UnityEngine;
using NUnit.Framework;
using System.IO;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Media.Blackmagic.Tests
{
    [Category("HardwareDependent")]
    [Category("GPUDependent")]
    [ExecuteInEditMode]
    class ShaderTests
    {
        public Texture2D Source;
        public RenderTexture Output;
        public Color[] SourcePix;

        [OneTimeSetUp]
        public void SetUp()
        {
            // Initialize ColorTransform
#if UNITY_EDITOR
            OutputColorTransform.AddShadersToSettings();
            InputColorTransform.AddShadersToSettings();
#endif

            // Create a base texture to compare against in the pack/unpack workflow
            var filePath = Path.GetFullPath("Packages/com.unity.media.blackmagic/Tests/ShaderTests/Adam.png");
            Assert.IsTrue(File.Exists(filePath));
            if (File.Exists(filePath))
            {
                Source = new Texture2D(200, 50, TextureFormat.RGBAFloat, false);
                Source.filterMode = FilterMode.Point;

                /* Load Image */
                //var data = File.ReadAllBytes(filePath);
                //Assert.IsTrue(Source.LoadImage(data));
                //Source.Apply();

                /* Generate Synthethic Texture */
                for (int i = 0; i < 200; ++i)
                {
                    for (int j = 0; j < 50; ++j)
                    {
                        float v = (float)((25 + i) / 256.0); // colors from 25 - 225
                        Source.SetPixel(i, j, new Color(v, v, v, 1));
                    }
                }
                Source.Apply();
            }

            Assert.IsNotNull(Source);
            Assert.IsFalse(Source.height == 2 || Source.height == 2);
            SourcePix = Source.GetPixels();

            Output = new RenderTexture(Source.width, Source.height, 0, GraphicsFormat.R16G16B16A16_SFloat);
            Output.filterMode = FilterMode.Point;
            // Utility function to visually debug
            //WriteAsPNG("Processed Source", Source);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            OutputColorTransform.Reset();
            InputColorTransform.Reset();

            RenderTexture.active = null;
            Object.DestroyImmediate(Output);
            Object.DestroyImmediate(Source);
            Object.DestroyImmediate(Packed);
        }

        private RenderTexture Packed;

        private bool ValidateOneSymmetry(BMDPixelFormat pixelFormat, BMDColorSpace colorSpace, bool convertToWorkingSpace, bool requiresLinearConversion)
        {
            // Assume worse dimensions to avoid loading Library
            if (Packed == null)
                Packed = new RenderTexture(Mathf.Max(Source.width * 2, 64), Source.height, 0, GraphicsFormat.R8G8B8A8_UNorm);

            // Pack, then unpack
            var outputMaterial = OutputColorTransform.Get(pixelFormat, colorSpace, BMDTransferFunction.HLG, convertToWorkingSpace, requiresLinearConversion);

            // Blit overrides the active textures, and must be restored
            var act = RenderTexture.active;
            Graphics.Blit(Source, Packed, outputMaterial, 0);
            var inputMaterial = InputColorTransform.Get(pixelFormat, colorSpace, BMDTransferFunction.HLG, convertToWorkingSpace, requiresLinearConversion);
            Graphics.Blit(Packed, Output, inputMaterial, 0);
            RenderTexture.active = act;

            // Compare pixels
            Assert.IsTrue(Source.width == Output.width && Source.height == Output.height);
            var outTex = new Texture2D(Output.width, Source.height, TextureFormat.RGBAFloat, false);
            var tmp = RenderTexture.active;
            RenderTexture.active = Output;
            outTex.ReadPixels(new Rect(0, 0, Output.width, Source.height), 0, 0);
            outTex.Apply();
            Color[] outputPix = outTex.GetPixels();
            RenderTexture.active = tmp; // restore static

            Assert.IsTrue(SourcePix.Length == outputPix.Length);

            // Sum represents the absolute average delta between the original and the output
            double sum = 0;

            int worseIndex = 0;
            var worseValue = 0.0;
            for (var i = 0; i < SourcePix.Length; ++i)
            {
                var diff = SourcePix[i] - outputPix[i];
                var current = ((Math.Abs(diff.r) + Math.Abs(diff.g) + Math.Abs(diff.b))) / 3;
                if (current > worseValue)
                {
                    worseIndex = i;
                    worseValue = current;
                }
                sum += current;
            }
            sum /= SourcePix.Length;

            // Threshold of acceptance
            var maxDeviation = 2.0 / 256; // this represent 2 values in 8 bits.

            // 8bits rgb is not symmetrical.
            var is8bitsRGB = pixelFormat == BMDPixelFormat.ARGB8Bit || pixelFormat == BMDPixelFormat.BGRA8Bit;
            // We accept 1/256 discrepancy on normal symmetry tests, 2/256 on conversion.
            var passed = is8bitsRGB || (sum < (convertToWorkingSpace || pixelFormat == BMDPixelFormat.YUV8Bit ? maxDeviation * 2 : maxDeviation));
            var passLabel = passed ? "PASSED" : "*FAILED";
            var pfLabel = Enum.GetName(typeof(BMDPixelFormat), pixelFormat);
            var csLabel = Enum.GetName(typeof(BMDColorSpace), colorSpace);
            var convertToWSLabel = convertToWorkingSpace ? "To 709" : "As is";
            var linConvLabel = requiresLinearConversion ? "WS Gamma" : "WS Lin";

            var quality = sum * 256 / 2.0; // Quality is the avg number of 8 bit values off for 1 conversion
            quality = (float)Math.Round(quality * 1000f) / 1000f; // 3 decimal places
            var worseColorDiff = SourcePix[worseIndex] - outputPix[worseIndex];

            Debug.Log(
                $"{passLabel}: {pfLabel} / {csLabel} / {convertToWSLabel} / {linConvLabel}. Quality: {quality}/256."
                + $" Worse : ({Math.Round(Math.Max(Math.Max(worseColorDiff.r * 256, worseColorDiff.g * 256), worseColorDiff.b * 256) * 1000f) / 1000f})"
            //+ $" Source : ({SourcePix[worseIndex].r*256}, {SourcePix[worseIndex].g*256}, {SourcePix[worseIndex].b*256})"
            //+ $" Sym : ({outputPix[worseIndex].r*256}, {outputPix[worseIndex].g*256}, {outputPix[worseIndex].b*256})"
            );

            if (!passed)
            {
                // Utility function to visually debug
                //WriteAsPNG($"Packed-{pfLabel}-{csLabel}-{convertToWSLabel}-{linConvLabel}", Packed);
                //WriteAsPNG($"Symmetry-{pfLabel}-{csLabel}-{convertToWSLabel}-{linConvLabel}", Output);
            }

            return passed;
        }

        [Test]
        public void ValidateSymmetry()
        {
            var ret = true;
            foreach (BMDPixelFormat format in Enum.GetValues(typeof(BMDPixelFormat)))
            {
                if (format != BMDPixelFormat.UseBestQuality)
                {
                    foreach (BMDColorSpace colorSpace in Enum.GetValues(typeof(BMDColorSpace)))
                    {
                        if (colorSpace == BMDColorSpace.UseDeviceSignal)
                        {
                            continue;
                        }

                        foreach (var reqLinConv in new[] { false, true })
                        {
                            foreach (var convertToWorkingSpace in new[] { false, true })
                            {
                                ret &= ValidateOneSymmetry(format, colorSpace, convertToWorkingSpace, reqLinConv);
                            }
                        }
                    }
                }
            }

            Assert.IsTrue(ret);
        }

        private void WriteAsPNG(string name, RenderTexture tex)
        {
            var filePath = Path.GetFullPath("Packages/com.unity.media.blackmagic/Tests/ShaderTests/");
            string outputName = filePath + name + ".png";

            Texture2D t2d = new Texture2D(tex.width, tex.height, TextureFormat.RGBAFloat, false);
            var tmp = RenderTexture.active;
            RenderTexture.active = tex;
            t2d.ReadPixels(new Rect(0, 0, t2d.width, t2d.height), 0, 0);
            t2d.Apply();
            byte[] bytes = t2d.EncodeToPNG();
            t2d.Apply();
            File.WriteAllBytes(outputName, bytes);
            RenderTexture.active = tmp;
        }

        private void WriteAsPNG(string name, Texture2D tex)
        {
            var filePath = Path.GetFullPath("Packages/com.unity.media.blackmagic/Tests/ShaderTests/");
            string outputName = filePath + name + ".png";
            byte[] bytes = tex.EncodeToPNG();
            tex.Apply();
            File.WriteAllBytes(outputName, bytes);
        }
    }
}
