# Available samples

The following four types of samples are currently available for Blackmagic Video. 

## Simple and advanced samples

These samples can be used with any graphic pipeline (**HDRP**, **URP**, and **Legacy**).

| **Sample:**      | **Description:**               |
| :----------------- | :-------------------------- |
| __Simple Input Output Plane Configuration__ | A simple way to Blit an incoming video feed (input device) to a Plane in the scene and output the result (output device). |
| __Simple External Keying Configuration__ | A basic configuration of an output device in the Blackmagic Window that shows how to use the External Keying feature. |
| __Simple Output Configuration__ | A basic configuration of an output device in the Blackmagic Window that shows how to output a Camera RenderTexture. |
| __Simple Output Audio Configuration__ | Outputs Unity's scene audio by using a Blackmagic output device.|
| __Simple Output Color Bars__ | A simple way to output color bars.|
| __Advanced Output Audio Configuration__ | Outputs Unity's scene audio by using a custom buffer and a Blackmagic output device. |
| __Advanced Output Canvas Configuration__ | Outputs a Unity scene with a Canvas on top. |

**Note**: Occasionally, the Shader used on an object is not detected correctly and can result in a Pink rendering. This happens when it was created on a specific Render Pipeline and is used on a different one. You can still use the sample by changing the Shader with another available one.

## HDRP samples

These samples can only be used with the **HDRP** pipeline.

| **Sample:**      | **Description:**               |
| :----------------- | :-------------------------- |
| __HDRP Input Fullscreen__ | HDRP way to blit an input video signal to the GameView by using a Blackmagic input device. |
| __HDRP Advanced Compositing (Graphics Compositor)__ | An advanced HDRP compositing example. Compositing is done by the **Graphics Compositor**, using the video input signal and the scene.|
| __HDRP Simple Compositing__ | A simple HDRP compositing example. The Compositing is done by a custom pass, using the video input signal and the scene. |

## Legacy samples

These samples can only be used with the **Legacy** pipeline.

| **Sample:**      | **Description:**               |
| :----------------- | :-------------------------- |
| __Legacy Input Fullscreen__ | Legacy way to blit an input video signal to the GameView by using a Blackmagic input device. |
| __Legacy Simple Compositing__ | A simple Legacy compositing example. The compositing is done by a Monobehaviour and OnRenderImage override, using the video input signal and the scene.|

## URP samples

These samples can only be used with the **URP** pipeline.

| **Sample:**      | **Description:**               |
| :----------------- | :-------------------------- |
| __URP Input Fullscreen__ | URP way to blit an input video signal to the GameView by using a Blackmagic input device. |
| __URP Simple Compositing__ | A simple URP compositing example. The compositing is done by the Renderer Feature, using the video input signal and the scene.|