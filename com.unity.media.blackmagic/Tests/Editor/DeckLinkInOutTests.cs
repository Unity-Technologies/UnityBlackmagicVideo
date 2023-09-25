using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Media.Blackmagic.Tests
{
    [Category(Contents.k_DefaultCategory)]
    class InOutFormatTests
    {
        GameObject m_Root;
        DeckLinkInputDevice m_InputDevice;
        GameObject m_FrameSenderGameObject;
        GameObject m_CameraGameObject;

        DeckLinkOutputDevice m_OutputDevice;
        Camera m_CameraComponent;

        [OneTimeSetUp]
        public void SetUp()
        {
            EditorSceneManager.OpenScene(AssetDatabase.GetAllAssetPaths().FirstOrDefault(x => x.EndsWith("BlackmagicTests.unity")));
            m_Root = new GameObject("Test Receiver GameObject", typeof(DeckLinkInputDevice));
            m_InputDevice = m_Root.GetComponent<DeckLinkInputDevice>();

            m_FrameSenderGameObject = new GameObject("Test Sender GameObject", typeof(DeckLinkOutputDevice));
            m_CameraGameObject = new GameObject("Test Camera GameObject", typeof(Camera));

            m_OutputDevice = m_FrameSenderGameObject.GetComponent<DeckLinkOutputDevice>();
            m_CameraComponent = m_CameraGameObject.GetComponent<Camera>();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(m_Root);
            GameObject.DestroyImmediate(m_FrameSenderGameObject);
            GameObject.DestroyImmediate(m_CameraGameObject);
        }

        [UnityTest]
        public IEnumerator InjectInValuesToOut()
        {
            Assert.IsNotNull(m_InputDevice);

            m_InputDevice.DeviceSelection = 0;
            m_InputDevice.UpdateInEditor = true;

#if UNITY_EDITOR
            EditorApplication.QueuePlayerLoopUpdate();
#endif

            // Necessary, so all logical devices are retrieved from the C++ API callback.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsNotNull(m_OutputDevice);

            m_OutputDevice.DeviceSelection = 0;
            m_CameraComponent.allowMSAA = false;
            m_OutputDevice.m_TargetCamera = m_CameraComponent;
            m_OutputDevice.UpdateInEditor = true;
            m_OutputDevice.m_RequiresReinit = false;

            yield return null;

            Assert.IsTrue(m_InputDevice.IsActive);
            Assert.IsTrue(m_OutputDevice.IsActive);

            m_InputDevice.ChangeColorSpace(BMDColorSpace.BT2020);
            m_InputDevice.ChangePixelFormat(BMDPixelFormat.YUV10Bit);
            m_InputDevice.ChangeTransferFunction(BMDTransferFunction.HLG);

            // Must be true, we are reinitializing the device.
            Assert.IsTrue(m_InputDevice.UpdateSettings);
            yield return null;

            m_OutputDevice.ChangeColorSpace(m_InputDevice.InColorSpace);
            m_OutputDevice.ChangePixelFormat(m_InputDevice.PixelFormat);
            m_OutputDevice.ChangeTransferFunction(m_InputDevice.TransferFunction);

            // Must be true, we are reinitializing the device.
            Assert.IsTrue(m_OutputDevice.UpdateSettings);
            yield return null;

            Assert.IsTrue(m_OutputDevice.InColorSpace == m_InputDevice.InColorSpace);
            Assert.IsTrue(m_OutputDevice.PixelFormat == m_InputDevice.PixelFormat);
            Assert.IsTrue(m_OutputDevice.TransferFunction == m_InputDevice.TransferFunction);
        }
    }
}
