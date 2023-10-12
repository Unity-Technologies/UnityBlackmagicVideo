using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Assertions;
using UnityEngine.LowLevel;
#if UNITY_EDITOR
using UnityEditor.Media;
#endif

namespace Unity.Media.Blackmagic
{
    /// <summary>
    /// The class responsible for coordinating multiple instance of DeckLink devices and for triggering their updates.
    /// </summary>
    static class VideoIOFrameManager
    {
        struct DeckLinkUpdate
        {
        }

        static int s_NextInputDeviceIndex = 0;
        static int s_NextOutputDeviceIndex = 0;
        static Dictionary<int, DeckLinkOutputDevice> s_FrameOutputDevices = new Dictionary<int, DeckLinkOutputDevice>();
        static Dictionary<int, DeckLinkInputDevice> s_FrameInputDevices = new Dictionary<int, DeckLinkInputDevice>();

        internal static bool GetOutputDevice(int key, out DeckLinkOutputDevice device)
        {
            return s_FrameOutputDevices.TryGetValue(key, out device) && device != null;
        }

        internal static bool GetInputDevice(int key, out DeckLinkInputDevice device)
        {
            return s_FrameInputDevices.TryGetValue(key, out device) && device != null;
        }

        /// <summary>
        /// Registers an output device instance in the manager.
        /// </summary>
        /// <param name="outputDevice">The output device component to register.</param>
        /// <returns>The index of the device.</returns>
        internal static int Register(DeckLinkOutputDevice outputDevice)
        {
            var deviceIndex = s_NextOutputDeviceIndex++;
            s_FrameOutputDevices.Add(deviceIndex, outputDevice);
            OnRegister();
            return deviceIndex;
        }

        /// <summary>
        /// Unregisters an output device instance in the manager.
        /// </summary>
        /// <param name="outputDevice">The output device component to unregister.</param>
        internal static void Unregister(DeckLinkOutputDevice outputDevice)
        {
            s_FrameOutputDevices.Remove(outputDevice.DeviceIndex);
            OnUnregister();
        }

        /// <summary>
        /// Registers an input device instance in the Manager.
        /// </summary>
        /// <param name="inputDevice">The input device component to register.</param>
        /// <returns>The index of the device.</returns>
        internal static int Register(DeckLinkInputDevice inputDevice)
        {
            var deviceIndex = s_NextInputDeviceIndex++;
            s_FrameInputDevices.Add(deviceIndex, inputDevice);
            OnRegister();
            return deviceIndex;
        }

        /// <summary>
        /// Unregisters an input device instance in the Manager.
        /// </summary>
        /// <param name="inputDevice">The input device component to unregister.</param>
        internal static void Unregister(DeckLinkInputDevice inputDevice)
        {
            s_FrameInputDevices.Remove(inputDevice.DeviceIndex);
            OnUnregister();
        }

        static void OnRegister()
        {
            if (s_FrameOutputDevices.Count + s_FrameInputDevices.Count == 1)
            {
                InjectSyncPointInPlayerLoop();
            }
        }

        static void OnUnregister()
        {
            if (s_FrameOutputDevices.Count + s_FrameInputDevices.Count == 0)
            {
                RemoveSyncPointFromPlayerLoop();
            }
        }

        static void CheckVSync()
        {
            // Question: Should we force this to vsync off rather than check that the user knows what they are doing?
            if (QualitySettings.vSyncCount != 0)
            {
                Debug.LogWarning("QualitySettings.VSyncCount has been disabled to maintain framerate.");
                QualitySettings.vSyncCount = 0;
            }
        }

        // Called just before Game time is updated for the next frame.
        static void PlayerLoopUpdate()
        {
            foreach (var inputDevice in s_FrameInputDevices)
            {
                inputDevice.Value.PerformUpdate();
            }

            // Give a chance for all the OutputDevices to capture the last rendered frame and perform output on the jack.
            // This should throttle the render rate if the device output queue is full.
            foreach (var outputDevice in s_FrameOutputDevices)
            {
                outputDevice.Value.PerformUpdate();
            }

            // When LC package is available use the genlock system it provides instead
#if !LIVE_CAPTURE_4_0_0_OR_NEWER && UNITY_EDITOR
            // Ready to start rendering next frame.
            if (s_FrameOutputDevices.Count > 0)
            {
                // Check that vsync is off (otherwise this can interfere with render rate)
                CheckVSync();

                // Verify everyone is working at the same rate and sync mode.
                // FIXME: this is a slow loop now... should probably only be checked when formats change.
                var fps = new MediaRational(0, 0);
                var syncMode = default(DeckLinkOutputDevice.SyncMode ? );

                foreach (var outputDeviceMap in s_FrameOutputDevices)
                {
                    var outputDevice = outputDeviceMap.Value;

                    if (!outputDevice.IsActive || outputDevice.m_Plugin == null)
                        continue;

                    if (!fps.isValid)
                    {
                        fps = outputDevice.FrameRate;
                    }
                    else if (!fps.Equals(outputDevice.FrameRate))
                    {
                        throw new InvalidOperationException("All OutputDevices must have compatible frame rates selected.");
                    }

                    if (syncMode == null)
                    {
                        syncMode = outputDevice.CurrentSyncMode;
                    }
                    else if (syncMode != outputDevice.CurrentSyncMode)
                    {
                        throw new InvalidOperationException("OutputDevices must all be set to either Manual or Async mode.");
                    }
                }

                if (syncMode == DeckLinkOutputDevice.SyncMode.ManualMode)
                {
                    // Have game time advance by exactly the right time step.
                    var frameDuration = (float)fps.denominator / (float)fps.numerator;
                    Time.captureDeltaTime = frameDuration;
                }
                else
                {
                    // Probably shouldn't override this, but it's easier to evaluate that ASync mode is doing what we
                    // want outside of "capture" mode
                    Time.captureDeltaTime = 0;
                }
            }
#endif
        }

        static void InjectSyncPointInPlayerLoop()
        {
            // Inject into player loop
            var newLoop = PlayerLoop.GetCurrentPlayerLoop();
            Assert.IsTrue(newLoop.subSystemList != null && newLoop.subSystemList.Length > 0);

            var initList = newLoop.subSystemList[0].subSystemList.ToList();

            Assert.IsFalse(initList.Any((x) => x.type == typeof(DeckLinkUpdate)), $"Player loop already has a {nameof(DeckLinkUpdate)} entry registered.");

            // inserting as the first subsystem ensures this executes before the live capture sync manager
            initList.Insert(0, new PlayerLoopSystem
            {
                type = typeof(DeckLinkUpdate),
                updateDelegate = PlayerLoopUpdate,
            });

            newLoop.subSystemList[0].subSystemList = initList.ToArray();
            PlayerLoop.SetPlayerLoop(newLoop);
        }

        static void RemoveSyncPointFromPlayerLoop()
        {
            var newLoop = PlayerLoop.GetCurrentPlayerLoop();
            Assert.IsTrue(newLoop.subSystemList != null && newLoop.subSystemList.Length > 0);

            var initList = newLoop.subSystemList[0].subSystemList.ToList();

            initList.RemoveAll(i => i.type == typeof(DeckLinkUpdate));

            newLoop.subSystemList[0].subSystemList = initList.ToArray();
            PlayerLoop.SetPlayerLoop(newLoop);
        }
    }
}
