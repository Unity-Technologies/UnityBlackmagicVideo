using UnityEngine;
using NUnit.Framework;
using UnityEngine.TestTools;
using System.Collections;
using System.Linq;
using Unity.Media.Blackmagic;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEditor;
#endif

namespace Unity.Media.Blackmagic.Tests
{
    class DeckLinkOutputTests
    {
        GameObject m_FrameSenderGameObject;
        GameObject m_CameraGameObject;

        DeckLinkOutputDevice m_OutputDevice;
        Camera m_CameraComponent;

        [OneTimeSetUp]
        public void SetUp()
        {
            EditorSceneManager.OpenScene(AssetDatabase.GetAllAssetPaths().FirstOrDefault(x => x.EndsWith("BlackmagicTests.unity")));

            m_FrameSenderGameObject = new GameObject("Test Sender GameObject", typeof(DeckLinkOutputDevice));
            m_CameraGameObject = new GameObject("Test Camera GameObject", typeof(Camera));

            m_OutputDevice = m_FrameSenderGameObject.GetComponent<DeckLinkOutputDevice>();
            m_CameraComponent = m_CameraGameObject.GetComponent<Camera>();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(m_FrameSenderGameObject);
            GameObject.DestroyImmediate(m_CameraGameObject);
        }

        [Test, Order(1)]
        public void CanAssignCameraTarget()
        {
            Assert.IsNotNull(m_OutputDevice);
            m_CameraComponent.allowMSAA = false;
            m_OutputDevice.m_TargetCamera = m_CameraComponent;
        }

        [UnityTest, Order(2)]
        public IEnumerator ResourcesAreInitialized()
        {
            Assert.IsNotNull(m_OutputDevice);

            m_OutputDevice.DeviceSelection = 0;
            m_OutputDevice.m_TargetCamera = Camera.main;
            m_OutputDevice.UpdateInEditor = true;
            m_OutputDevice.m_RequiresReinit = false;

#if UNITY_EDITOR
            EditorApplication.QueuePlayerLoopUpdate();
#endif
            yield return null;

            Assert.IsNotNull(m_OutputDevice.m_Plugin);
        }

        [Test, Order(3)]
        public void CanPromoteToManual()
        {
            Assert.IsNotNull(m_OutputDevice);
            Assert.IsNotNull(m_OutputDevice.m_Plugin);

            m_OutputDevice.PromoteToManualMode();

            Assert.IsTrue(DeckLinkOutputDevice.ManualModeInstance == m_OutputDevice);
        }

        [Test, Order(4)]
        public void EncodedFrameIsEnqueued()
        {
            Assert.IsNotNull(m_OutputDevice);
            Assert.IsNotNull(m_OutputDevice.m_Plugin);

            var dimensions = m_OutputDevice.FrameDimensions;
            var renderTexture = m_OutputDevice.TargetTexture;

            if (dimensions.x > 1 && dimensions.y > 1)
            {
                Assert.IsNotNull(renderTexture);
            }

            var initialCount = m_OutputDevice.m_FrameQueue.Count;
            const int CountTest = 4;
            for (int i = 0; i < CountTest; ++i)
            {
                m_OutputDevice.EncodeFrameAndAddToQueue(renderTexture);
            }

            var count = (m_OutputDevice.m_Plugin.IsProgressive) ? CountTest : (CountTest / 2);

            if (dimensions.x > 1 && dimensions.y > 1)
            {
                Assert.IsTrue(m_OutputDevice.m_FrameQueue.Count == initialCount + count);
            }
            else
            {
                Assert.IsTrue(m_OutputDevice.m_FrameQueue.Count == initialCount);
            }
        }

        [UnityTest, Order(5)]
        public IEnumerator ResourcesAreDisposed()
        {
            m_OutputDevice.UpdateInEditor = false;
            m_OutputDevice.m_Initialized = true;
            m_OutputDevice.m_LifeCycleNeedsUpdate = true;

#if UNITY_EDITOR
            EditorApplication.QueuePlayerLoopUpdate();
#endif
            yield return null;

            Assert.IsNotNull(m_OutputDevice);

            if (m_OutputDevice.CurrentSyncMode == DeckLinkOutputDevice.SyncMode.ManualMode)
            {
                Assert.IsNull(DeckLinkOutputDevice.ManualModeInstance);
            }

            Assert.IsNull(m_OutputDevice.TargetTexture);
            Assert.IsNull(m_OutputDevice.m_Plugin);
        }
    }
}
