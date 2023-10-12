using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

using UnityEditor;
using UnityEditor.SceneManagement;

using static Unity.Media.Blackmagic.DeckLinkConnectorMapping;
using InputData = System.Tuple<string, int>;

namespace Unity.Media.Blackmagic.Tests
{
    [Category(Contents.k_DefaultCategory)]
    class DeckLinkManagerTestsHardware
    {
        class ManagerContents
        {
            public const string k_DetectionError = "Detection error";

            public static readonly DeckLinkConnectorMapping[] k_CompatibleMappings =
            {
                FourSubDevicesHalfDuplex,
                OneSubDeviceFullDuplex,
                OneSubDeviceHalfDuplex,
                TwoSubDevicesFullDuplex
            };
        }

        class FourSubDevicesHalfDuplexConfiguration
        {
            public const int k_CompatibleLinkModes = 1;
            public const int k_CompatibleKeyingModes = 0;
            public const bool k_IsLinkModeCompatible = false;
            public const bool k_IsKeyingCompatible = false;
        }

        class OneSubDeviceFullDuplexConfiguration
        {
            public const int k_CompatibleLinkModes = 3;
            public const int k_CompatibleKeyingModes = 6;
            public const bool k_IsLinkModeCompatible = true;
            public const bool k_IsKeyingCompatible = true;
        }

        class OneSubDeviceHalfDuplexConfiguration
        {
            public const int k_CompatibleLinkModes = 7;
            public const int k_CompatibleKeyingModes = 6;
            public const bool k_IsLinkModeCompatible = true;
            public const bool k_IsKeyingCompatible = true;
        }

        class TwoSubDeviceFullDuplexConfiguration
        {
            public const int k_CompatibleLinkModes = 1;
            public const int k_CompatibleKeyingModes = 6;
            public const bool k_IsLinkModeCompatible = false;
            public const bool k_IsKeyingCompatible = true;
        }

        class TwoSubDeviceHalfDuplexConfiguration
        {
            public const int k_CompatibleLinkModes = 1;
            public const int k_CompatibleKeyingModes = 6;
            public const bool k_IsLinkModeCompatible = true;
            public const bool k_IsKeyingCompatible = false;
        }

        DeckLinkManager m_DeckLinkManager;

        void ChangeConnectorMapping(DeckLinkConnectorMapping mapping)
        {
            var cardIndex = m_DeckLinkManager.deckLinkCardIndex;
            m_DeckLinkManager.m_DevicesConnectorMapping[cardIndex] = mapping;
            m_DeckLinkManager.MappingConnectorProfileChanged(mapping, cardIndex);
        }

        [Category(Contents.k_DefaultCategory)]
        [OneTimeSetUp]
        public void SetUp()
        {
            var scene = EditorSceneManager.OpenScene(AssetDatabase.GetAllAssetPaths().FirstOrDefault(x => x.EndsWith(Contents.k_DefaultScene)));
            Assert.IsNotNull(scene);
        }

        [Category(Contents.k_DefaultCategory)]
        [Test, Order(1)]
        public void IsVideoManagerValid()
        {
            var managerFound = DeckLinkManager.TryGetInstance(out var manager);
            if (managerFound)
            {
                m_DeckLinkManager = manager;
            }

            Assert.IsTrue(managerFound);
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(DeckLinkManager.s_DeckLinkDeviceDiscovery != IntPtr.Zero);
        }

        [Category(Contents.k_DefaultCategory)]
        [Test, Order(2)]
        public void IsHardwareDeviceDetected()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsNotNull(m_DeckLinkManager.activeDeckLinkCard);

            var hardwareModel = m_DeckLinkManager.activeDeckLinkCard.name;
            Assert.IsTrue(hardwareModel.CompareTo(ManagerContents.k_DetectionError) != 0);
        }

        [Category(Contents.k_DefaultCategory)]
        [Test, Order(3)]
        public void IsHardwareAPIDetected()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_DeckLinkManager.ApiVersion.CompareTo(ManagerContents.k_DetectionError) != 0);
        }

        [Category(Contents.k_DefaultCategory)]
        [UnityTest, Order(4)]
        public IEnumerator CanEnableAndDisableVideoManager()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            DeckLinkManager.EnableVideoManager = false;
            yield return null;
            Assert.IsFalse(m_DeckLinkManager.enabled);

            DeckLinkManager.EnableVideoManager = true;
            yield return null;
            Assert.IsTrue(m_DeckLinkManager.enabled);
        }

        [Category(Contents.k_DefaultCategory)]
        [UnityTest, Order(5)]
        public IEnumerator CanChangeTheConnectorMappingToOneSubDeviceFullDuplex()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            ChangeConnectorMapping(OneSubDeviceFullDuplex);

            // Necessary, so all logical devices are retrieved from the C++ API callback.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsTrue(m_DeckLinkManager.connectorMapping == OneSubDeviceFullDuplex);
            Assert.IsTrue(m_DeckLinkManager.IsCurrentMappingCompatible());
        }

        [Category(Contents.k_DefaultCategory)]
        [Test, Order(6)]
        public void IsLinkModeCompatibleWithOneSubDeviceFullDuplex()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_DeckLinkManager.connectorMapping == OneSubDeviceFullDuplex);

            var activeCard = m_DeckLinkManager.activeDeckLinkCard;
            Assert.IsNotNull(activeCard);

            Assert.IsTrue(activeCard.compatibleLinkModes == OneSubDeviceFullDuplexConfiguration.k_CompatibleLinkModes);
            Assert.IsTrue(activeCard.isLinkModeCompatible == OneSubDeviceFullDuplexConfiguration.k_IsLinkModeCompatible);
        }

        [Category(Contents.k_DefaultCategory)]
        [Test, Order(7)]
        public void IsKeyingCompatibleWithOneSubDeviceFullDuplex()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_DeckLinkManager.connectorMapping == OneSubDeviceFullDuplex);

            var activeCard = m_DeckLinkManager.activeDeckLinkCard;
            Assert.IsNotNull(activeCard);

            Assert.IsTrue(activeCard.compatibleKeyingModes == OneSubDeviceFullDuplexConfiguration.k_CompatibleKeyingModes);
            Assert.IsTrue(activeCard.isKeyingCompatible == OneSubDeviceFullDuplexConfiguration.k_IsKeyingCompatible);
        }

        [Category(Contents.k_DefaultCategory)]
        [UnityTest, Order(8)]
        public IEnumerator CanChangeTheConnectorMappingToOneSubDeviceHalfDuplex()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            ChangeConnectorMapping(OneSubDeviceHalfDuplex);

            // Necessary, so all logical devices are retrieved from the C++ API callback.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsTrue(m_DeckLinkManager.connectorMapping == OneSubDeviceHalfDuplex);
            Assert.IsTrue(m_DeckLinkManager.IsCurrentMappingCompatible());
        }

        [Category(Contents.k_DefaultCategory)]
        [Test, Order(9)]
        public void IsLinkModeCompatibleWithOneSubDeviceHalfDuplex()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_DeckLinkManager.connectorMapping == OneSubDeviceHalfDuplex);

            var activeCard = m_DeckLinkManager.activeDeckLinkCard;
            Assert.IsNotNull(activeCard);

            Assert.IsTrue(activeCard.compatibleLinkModes == OneSubDeviceHalfDuplexConfiguration.k_CompatibleLinkModes);
            Assert.IsTrue(activeCard.isLinkModeCompatible == OneSubDeviceHalfDuplexConfiguration.k_IsLinkModeCompatible);
        }

        [Category(Contents.k_DefaultCategory)]
        [Test, Order(10)]
        public void IsKeyingCompatibleWithOneSubDeviceHalfDuplex()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_DeckLinkManager.connectorMapping == OneSubDeviceHalfDuplex);

            var activeCard = m_DeckLinkManager.activeDeckLinkCard;
            Assert.IsNotNull(activeCard);

            Assert.IsTrue(activeCard.compatibleKeyingModes == OneSubDeviceHalfDuplexConfiguration.k_CompatibleKeyingModes);
            Assert.IsTrue(activeCard.isKeyingCompatible == OneSubDeviceHalfDuplexConfiguration.k_IsKeyingCompatible);
        }

        [Category(Contents.k_DefaultCategory)]
        [UnityTest, Order(11)]
        public IEnumerator CanChangeTheConnectorMappingToTwoSubDevicesFullDuplex()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            ChangeConnectorMapping(TwoSubDevicesFullDuplex);

            // Necessary, so all logical devices are retrieved from the C++ API callback.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsTrue(m_DeckLinkManager.connectorMapping == TwoSubDevicesFullDuplex);
            Assert.IsTrue(m_DeckLinkManager.IsCurrentMappingCompatible());
        }

        [Category(Contents.k_DefaultCategory)]
        [Test, Order(12)]
        public void IsLinkModeCompatibleWithTwoSubDevicesFullDuplex()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_DeckLinkManager.connectorMapping == TwoSubDevicesFullDuplex);

            var activeCard = m_DeckLinkManager.activeDeckLinkCard;
            Assert.IsNotNull(activeCard);

            Assert.IsTrue(activeCard.compatibleLinkModes == TwoSubDeviceFullDuplexConfiguration.k_CompatibleLinkModes);
            Assert.IsTrue(activeCard.isLinkModeCompatible == TwoSubDeviceFullDuplexConfiguration.k_IsLinkModeCompatible);
        }

        [Category(Contents.k_DefaultCategory)]
        [Test, Order(13)]
        public void IsKeyingCompatibleWithTwoSubDevicesFullDuplex()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_DeckLinkManager.connectorMapping == TwoSubDevicesFullDuplex);

            var activeCard = m_DeckLinkManager.activeDeckLinkCard;
            Assert.IsNotNull(activeCard);

            Assert.IsTrue(activeCard.compatibleKeyingModes == TwoSubDeviceFullDuplexConfiguration.k_CompatibleKeyingModes);
            Assert.IsTrue(activeCard.isKeyingCompatible == TwoSubDeviceFullDuplexConfiguration.k_IsKeyingCompatible);
        }

        [Category(Contents.k_DefaultCategory)]
        [UnityTest, Order(14)]
        public IEnumerator CanChangeTheConnectorMappingToTwoSubDevicesHalfDuplex()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            ChangeConnectorMapping(TwoSubDevicesHalfDuplex);

            // Necessary, so all logical devices are retrieved from the C++ API callback.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsTrue(m_DeckLinkManager.connectorMapping == TwoSubDevicesHalfDuplex);
            Assert.IsFalse(m_DeckLinkManager.IsCurrentMappingCompatible());
        }

        [Category(Contents.k_DefaultCategory)]
        [Test, Order(15)]
        public void IsLinkModeCompatibleWithTwoSubDevicesHalfDuplex()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_DeckLinkManager.connectorMapping == TwoSubDevicesHalfDuplex);

            var activeCard = m_DeckLinkManager.activeDeckLinkCard;
            Assert.IsNotNull(activeCard);

            Assert.IsTrue(activeCard.compatibleLinkModes == TwoSubDeviceHalfDuplexConfiguration.k_CompatibleLinkModes);

            // True by default when the profile has not been changed efficiently.
            Assert.IsTrue(activeCard.isLinkModeCompatible == TwoSubDeviceHalfDuplexConfiguration.k_IsLinkModeCompatible);
        }

        [Category(Contents.k_DefaultCategory)]
        [Test, Order(16)]
        public void IsKeyingCompatibleWithTwoSubDevicesHalfDuplex()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_DeckLinkManager.connectorMapping == TwoSubDevicesHalfDuplex);

            var activeCard = m_DeckLinkManager.activeDeckLinkCard;
            Assert.IsNotNull(activeCard);

            Assert.IsTrue(activeCard.compatibleKeyingModes == TwoSubDeviceHalfDuplexConfiguration.k_CompatibleKeyingModes);

            // False by default, because the connector mapping is not compatible.
            Assert.IsTrue(activeCard.isKeyingCompatible == TwoSubDeviceHalfDuplexConfiguration.k_IsKeyingCompatible);
        }

        [Category(Contents.k_DefaultCategory)]
        [UnityTest, Order(17)]
        public IEnumerator CanChangeTheConnectorMappingToFourSubDevicesHalfDuplex()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            ChangeConnectorMapping(FourSubDevicesHalfDuplex);

            // Necessary, so all logical devices are retrieved from the C++ API callback.
            for (var initializationDelay = Contents.k_InitDelay; initializationDelay > 0; --initializationDelay)
                yield return null;

            Assert.IsTrue(m_DeckLinkManager.connectorMapping == FourSubDevicesHalfDuplex);
            Assert.IsTrue(m_DeckLinkManager.IsCurrentMappingCompatible());
        }

        [Category(Contents.k_DefaultCategory)]
        [Test, Order(18)]
        public void IsLinkModeCompatibleWithFourSubDevicesHalfDuplex()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_DeckLinkManager.connectorMapping == FourSubDevicesHalfDuplex);

            var activeCard = m_DeckLinkManager.activeDeckLinkCard;
            Assert.IsNotNull(activeCard);

            Assert.IsTrue(activeCard.compatibleLinkModes == FourSubDevicesHalfDuplexConfiguration.k_CompatibleLinkModes);
            Assert.IsTrue(activeCard.isLinkModeCompatible == FourSubDevicesHalfDuplexConfiguration.k_IsLinkModeCompatible);
        }

        [Category(Contents.k_DefaultCategory)]
        [Test, Order(19)]
        public void IsKeyingCompatibleWithFourSubDevicesHalfDuplex()
        {
            Assert.IsNotNull(m_DeckLinkManager);
            Assert.IsTrue(m_DeckLinkManager.connectorMapping == FourSubDevicesHalfDuplex);

            var activeCard = m_DeckLinkManager.activeDeckLinkCard;
            Assert.IsNotNull(activeCard);

            Assert.IsTrue(activeCard.compatibleKeyingModes == FourSubDevicesHalfDuplexConfiguration.k_CompatibleKeyingModes);
            Assert.IsTrue(activeCard.isKeyingCompatible == FourSubDevicesHalfDuplexConfiguration.k_IsKeyingCompatible);
        }

        [Category(Contents.k_DefaultCategory)]
        [Test, Order(20)]
        public void CheckIfTheConnectorMappingsAreCompatible()
        {
            Assert.IsNotNull(m_DeckLinkManager);

            var activeCard = m_DeckLinkManager.activeDeckLinkCard;

            var compatibleProfiles = activeCard.compatibleConnectorMappings;
            Assert.IsTrue(compatibleProfiles.Count > 0);

            foreach (var profile in ManagerContents.k_CompatibleMappings)
            {
                Assert.IsTrue(compatibleProfiles.Contains(profile));
            }
        }
    }
}
