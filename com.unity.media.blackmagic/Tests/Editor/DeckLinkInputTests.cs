using UnityEngine;
using NUnit.Framework;
using System.Collections;
using System.Linq;
using UnityEngine.TestTools;
using Unity.Media.Blackmagic;
using UnityEditor.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Media.Blackmagic.Tests
{
    class DeckLinkInputTests
    {
        GameObject m_Root;
        DeckLinkInputDevice m_InputDevice;

        [OneTimeSetUp]
        public void SetUp()
        {
            EditorSceneManager.OpenScene(AssetDatabase.GetAllAssetPaths().FirstOrDefault(x => x.EndsWith("BlackmagicTests.unity")));
            m_Root = new GameObject("Test Receiver GameObject", typeof(DeckLinkInputDevice));
            m_InputDevice = m_Root.GetComponent<DeckLinkInputDevice>();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(m_Root);
        }

        [Test]
        public void FormatNameIsNotNullOrEmpty()
        {
            Assert.IsNotNull(m_InputDevice.FormatName);
            Assert.IsFalse(m_InputDevice.FormatName == "");
        }

        [UnityTest, Order(1)]
        public IEnumerator ResourcesAreInitialized()
        {
            Assert.IsNotNull(m_InputDevice);

            m_InputDevice.DeviceSelection = 0;
            m_InputDevice.UpdateInEditor = true;
            m_InputDevice.m_RequiresReinit = false;

#if UNITY_EDITOR
            EditorApplication.QueuePlayerLoopUpdate();
#endif
            yield return null;

            Assert.IsNotNull(m_InputDevice.m_Plugin);
        }

        [UnityTest, Order(3)]
        public IEnumerator CheckResizeSourceRenderTexture()
        {
            Assert.IsNotNull(m_InputDevice);

            // Combinations to play with
            BMDPixelFormat[] formats = new BMDPixelFormat[] { BMDPixelFormat.YUV10Bit, BMDPixelFormat.YUV8Bit };
            int[] widths = new int[] { 1920, 1280, 225, 501 }; // Usual suspects + odd, cuz odd always is odd.
            int[] heights = new int[] { 1080, 720, 225, 111 };

            foreach (var format in formats)
            {
                m_InputDevice.m_RequestedInPixelFormat = format;

                yield return null; // Await initialization.

                Assert.IsNotNull(m_InputDevice.m_Plugin);

                //TO DO: I can't run this test on my machine, it's looping infinitely.
                if (!DeckLinkManager.TryGetInstance(out var deckLinkManager) || deckLinkManager.m_NoCardDetected)
                    yield break;

                foreach (var width in widths)
                {
                    foreach (var height in heights)
                    {
                        m_InputDevice.UpdateSourceTexture();

                        // Wait for the card to acknowledge format change.
                        while (m_InputDevice.PixelFormat != m_InputDevice.m_RequestedInPixelFormat)
                        {
                            yield return null;
                        }

                        m_InputDevice.UpdateSourceTexture();
                        Assert.IsNotNull(m_InputDevice.m_SourceTexture);

                        var w = m_InputDevice.PixelFormat.GetByteWidth(width);
                        var h = m_InputDevice.PixelFormat.GetByteHeight(height);
                        var d = m_InputDevice.PixelFormat.GetByteDepth();

                        const int depth = 4; // Right now, we only need support for 4 bytes backing. Validate.
                        Assert.IsTrue(d == depth);
                        Assert.Zero(w % depth);
                        Assert.IsNotNull(m_InputDevice.m_SourceTexture);
                        Assert.IsTrue(m_InputDevice.m_SourceTexture.width == w / depth);
                        Assert.IsTrue(m_InputDevice.m_SourceTexture.height == h);

                        Assert.IsTrue(m_InputDevice.m_SourceTexture.filterMode == FilterMode.Point);

                        yield return null;
                    }
                }
            }
        }

        [UnityTest, Order(6)]
        public IEnumerator ResourcesAreDisposed()
        {
            m_InputDevice.UpdateInEditor = false;
            yield return null;

            Assert.IsNotNull(m_InputDevice);

            Assert.IsNull(m_InputDevice.m_SourceTexture);
            Assert.IsNull(m_InputDevice.TargetTexture);
            Assert.IsNull(m_InputDevice.m_Plugin);
        }
    }
}
