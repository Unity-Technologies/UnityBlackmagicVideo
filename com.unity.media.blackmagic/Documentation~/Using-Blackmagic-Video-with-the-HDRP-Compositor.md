# Using Blackmagic Video with the HDRP Compositor

## Setting up the scene

The following setup is an example of how you can use the Graphics Compositor to create a simple Compositing.

1. In the project's HDRP settings, change the **Color Buffer Format** to `R16G16B16A16` in the Rendering and Post-Processing section.
2. Create a new **Compositor Graph** by going to `Window / Render Pipeline / Compositor Graph`.
3. Create three new cameras:

   a. A **scene camera** to render all objects in the scene.

   b. A **video renderer camera** for the input stream. It is projected on a Plane or directly Blitted in Fullscreen.

   c. A **final render camera** that captures the final composited result of the other two cameras.

4. Create a new layer (name it for example, "Video Compositing") and assign all game objects that will be rendered in front of the video.

The **Layer** `VideoCompositing` is set on multiple objects/volumes in order to achieve a correct Compositing in the [Graphics Compositor](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@10.2/manual/Compositor-User-Guide.html) tool. It helps to specify what the actual camera instance should render or not. 

You can find an example with the Custom Post Process **GrayScale** (on the `SceneVisual/GrayScale Volume` object), where the **Video Camera** uses it. The Gray Scale is only applied to the **input video** and not on the Cube.

The `VideoCompositing layer` is used to identify objects or volumes to be rendered by the video camera, that is, the camera responsible for rendering the incoming video feed.