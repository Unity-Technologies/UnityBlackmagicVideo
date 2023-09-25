using System;
using UnityEngine;

namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// This class represents the Video IO Device data.
    /// </summary>
    [Serializable]
    class VideoDeviceGameObjectData : IDisposable
    {
        /// <summary>
        /// The name of the device.
        /// </summary>
        public string Name;

        /// <summary>
        /// The video device component.
        /// </summary>
        public BaseDeckLinkDevice VideoDevice;

        /// <summary>
        /// The video device data.
        /// </summary>
        public IVideoDeviceData VideoData;

        /// <summary>
        /// The video device current index.
        /// </summary>
        public int CurrentDeviceIndex;

        /// <summary>
        /// The video device previous index.
        /// </summary>
        public int OldDeviceIndex;

        /// <summary>
        /// Releases the data resources.
        /// </summary>
        public void Dispose()
        {
            BlackmagicUtilities.Destroy(VideoDevice.gameObject);
            VideoDevice = null;
            CurrentDeviceIndex = -1;
            OldDeviceIndex = -1;
        }

        /// <summary>
        /// Changes the current and the previous video device index.
        /// </summary>
        /// <param name="index">Index of the current device.</param>
        /// <param name="oldIndex">Previous index of the current device.</param>
        public void SetCurrentVideoDevice(int index, int oldIndex = -1)
        {
            CurrentDeviceIndex = index;
            OldDeviceIndex = oldIndex;
            VideoDevice.OldDeviceSelection = oldIndex;
            VideoDevice.DeviceSelection = index;
        }
    }
}
