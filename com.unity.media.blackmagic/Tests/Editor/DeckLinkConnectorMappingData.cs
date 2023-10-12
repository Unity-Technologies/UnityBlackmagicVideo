using System;
using System.Collections.Generic;

using static Unity.Media.Blackmagic.DeckLinkConnectorMapping;

namespace Unity.Media.Blackmagic.Tests
{
    class Contents
    {
        public const string k_DefaultCategory = "HardwareDependent";
        public const string k_DefaultScene = "BlackmagicTests.unity";
        public const int k_InitDelay = 200;
        public const string k_SignalNotDefined = "Not defined";
        public const DeckLinkConnectorMapping k_DefaultConnectorMappingProfile = FourSubDevicesHalfDuplex;
    }
}
