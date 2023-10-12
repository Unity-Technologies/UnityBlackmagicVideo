# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [Unreleased] - 2023-09-08
### Changed
- Removed Pro License requirement.

## [2.0.1] - 2023-05-15
### Added
- Linux support.

### Changed
- Updated the SDK to 12.4.2.

### Fixed
- A bug that causes the device name to be wrong.
= A bug that causes the device dropdown (public API) to be inactive.

## [2.0.0] - 2023-03-15
### Added
- New integration with the Live Capture package genlock APIs.
- Added synchronized audio input callbacks.
- Editor analytics.
- An error message in the Blackmagic window when the SDI port is already used by another program.

### Changed
- Improved our interlace workflow.
- Improved our synchronization workflow.
- Buffering of frames is now in C# instead of C++.
- Updated the required minimum Unity Editor version to Unity 2022.2.
- Some input device API have been refactored to improve clarity and provide more detailed information.
- Removed the licensing error for Standalone builds.

### Fixed
- A crash when using an input device.
- A bug that causes the frame duration for the input device format to be incorrect.
- Removing input device while an output device was using it (Media Mode: Same as Input) was spamming errors in the console.
- Input device callbacks now work in IL2CPP builds.
- A crash when the SDI port is already used by another program (output).
- A crash due to an invalid configuration when using the RGB12 format.
- The Link Mode property was not persistently serialized.

## [1.0.1] - 2022-09-20
### Added
- An API to retrieve the status of a device.

### Changed
- Restricted the access of many classes and methods.
- Removed an unused Custom Pass in our HDRP sample project.
- Removed the Keyer assets and samples from the URP and Legacy sample projects.

### Fixed
- An error in the console when not using the Low Latency checkbox.

## [1.0.0] - 2022-08-09
### Added
- The capability to rename the custom dropdown of devices when using the public API.
- A public API to detect an incoming signal or not.
- Collapsible fields are now serialized.
- The Low Latency feature has been improved.
- A warning when using GPUDirect without Direct3D11.

### Changed
- The synchronization between the input audio and the video has been improved.

### Fixed
- A runtime error when using the Low Latency property.

## [1.0.0-pre.3] - 2022-06-20
### Changed
- Change licenses in the Third Party Notices file.

### Fixed
- A crash when using a multi-scenes project without a proper license.
- A crash when using RenderDoc in the editor.
- Missing shaders in the HDRP sample project.

## [1.0.0-pre.2] - 2022-05-05
### Changed
- Improve the public API documentation.

## [1.0.0-pre.1] - 2022-04-05
### Added
- A licensing solution, enforcing the use of a Unity Pro license.
- New sample scenes. They are also now available through the Package Manager.
- Hardware-dependent Unit Tests.
- An error callback that is accessible from the public API.
- Support for using multiple DeckLink cards at the same time.
- Support for GPUDirect technology.
- Input audio support.
- An audio callback to retrieve the audio data, accessible from the public API.
- A new output audio mode. It can automatically retrieve and use the audio data from an input device.

### Changed
- Improvements have been made regarding color accuracy (BT.2020 and BT.709).
- The package has been renamed to com.unity.media.blackmagic.
- The old Unity logo has been removed from the sample scenes.

### Fixed
- Compilation errors when installing the Blackmagic and the Live Capture packages in the same project.
- An issue where devices were being erased when disabling the Video Manager.
- macOS crashes when using BT.2020 in addition to the Async mode.
- macOS and Windows leaks when using the Async mode.
- Serialization issues with the color properties.
- Color accuracy in the Plane sample scene.
- The Link Mode property was not always detected correctly on macOS.

## [1.0.0-exp.3] - 2021-11-29
### Added
- The ability to change many properties in runtime.
- A manual compositing sample scene in the HDRP project.
- Multiple Color Difference keying sample scenes in the HDRP project.
- RGB12 for input and output devices.
- Sample scenes optimization.
- Timecode support for input and output devices.
- A sample project using the Timecode feature with the Live Capture package.
- A filtering mode for input and output RenderTexture.
- A warning when the same logical device is used twice.
- The capacity to select the Link mode (Single / Dual / Quad) on output devices.
- Support of IL2CPP.
- Multithreaded copy for input and output plugin operations.
- Public API functions to change multiple properties in Runtime, such as the Pixel Format or the Color Space information.
- The ability to override on input for Colorspace, Pixel Format, and Transfer function.
- Updated documentation.

### Changed
- The name of the Blackmagic window.
- The names of the sample projects.
- Detection of potential conflicts between the video source and the DeckLink card configuration.
- Input devices are now always initialized in YUV8 and then changed to the requested Pixel Format if compatible.
- Removed GC.Alloc.
- Revised the Blackmagic window (input and output sections).

### Fixed
- Memory leaks.
- Stuttering issues.
- Crashes in Async mode.
- Incompatibilities between Rec.2020 and External keying.
- Driver crashes when the Pixel Format isn't compatible with the DeckLink card used.
- Update in Editor issue after saving the scene.
- Serialization issue for Fill and Key.
- A URP leak issue.
- HDR multiple issues.
- URP's RenderTexture dark issue.

## [1.0.0-exp.2] - 2021-08-03
### Added
- Apple environment support.
- An icon next to each device, indicating whether its current use has problems or not.
- RGBA 8bits pixel format for the output devices.
- YUV 10bits pixel format for the input and output devices.
- Support of Internal and External Fill and Key.
- Interlacing and Deinterlacing for all pixel formats.
- Sample scenes for the Fill and Key feature.
- The ability to change the SDI port(s), video configuration, and pixel format while the device is in use.
- Auto-detection of the compatible Connector Mapping profiles for the installed DeckLink card.
- The support of the 4 main Connector Mapping profiles.
- The public API has been extended, offering more capabilities.
- Colorspaces metadata support for Rec.601, Rec.709, and Rec.2020.
- Icons for the Plugin window.

### Changed
- Unity's clock is now better synchronized with the DeckLink card's clock.
- Public API for accessing the target RenderTexture property.
- The video configuration is now separated in 3 different dropdowns (Resolution / Framerate / Scanning Mode).
- A second camera is no longer necessary, the Game View is now always rendering the Camera result.

### Fixed
- Editor errors when reloading the Plugin window.
- Error in console when going in Play mode with Editor Update checked.
- The SDI port(s) on the devices were not always reloaded correctly in Standalone Builds.
- The Camera Bridge feature on HDRP and the Graphics Compositor.
- Incorrect conversion when changing the video configuration.
- Interlacing and deinterlacing artifacts.
- Memory leaks.

## [1.0.0-exp.1] - 2021-06-05
### Added
- A button to remove the Window Devices instance and its resources in the scene.
- A button to warn the user that their color buffer format doesn't have an alpha channel.
- Multiple output devices support.
- Outputting to a format different than the fullscreen application format.
- Standalone build support.
- Public API to retrieve device(s) information.

### Changed
- A CustomPropertyDrawer now allows the user to easily access the public API functions.
- Output devices are not using the Target Texture property anymore, on the assigned Camera component.

### Fixed
- The right command buffer is now removed for output devices using the Legacy RenderPipeline.
- A leak issue, where the resources were not removed during an assembly reload.

## [0.0.1-preview.2] - 2021-02-19
### Added
- A 'Video Manager' window to manage Blackmagic devices and show related information to the plugin.
- 'Connector Mappings' to change how Blackmagic devices are mapped to the SDI ports. 
- Detection of the hardware (displayed in the Video Manager window).
- Detection of the API version (displayed in the Video Manager window).
- Automatic device detection through the C++ plugin (device added / removed / unplugged).
- Pixel Format for the Input and Output devices.
- Automatic detection of the DeckLinkInput video format changed. It flushes the queue and recreates it with the new format.
- Revamp of the C++ plugin. 
- Indention, norm, and Blackmagic namespaces.
- Documentation and Index.

### Fixed
- FrameReceiver is now named DeckLinkInputDevice.
- FrameSender is now named DeckLinkOutputDevice.
- The new Video Manager window is fixed and is working for all Sample scenes.

### Removed
- The ability to add devices in the scene. You should now use the new Video Manager window.
- Remove old and unused assets in the sample projects.

## [0.0.1-preview.1] - 2020-10-11
### This is the first release of *Blackmagic Video*.
### Added
- Blackmagic video receiver.
- Blackmagic video sender.
- Sample scenes.
- Indention, norm, and Blackmagic namespaces.
- Documentation and Index.
