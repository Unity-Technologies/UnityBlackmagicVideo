# Blackmagic Video

Use the Blackmagic Video package to leverage video capture cards from Blackmagic Design to input and output pro-grade video and audio in the Unity Editor and runtime builds, unlocking new workflows for users working in film, virtual production, live events, and broadcast.

For more information (installation, concepts, features, and workflows) read the [Blackmagic Video package documentation](Documentation~/index.md) in this repository.  

Review the specific [license terms](LICENSE.md) about this package.

## Requirements

* Windows 10 or macOS
* Blackmagic capture card
* Camera with SDI or HDMI output (depending on capture card)

## Blackmagic API plugin

The package uses a compiled assembly that provides access to Blackmagic's API.
* The files needed to build the plugin are in the [**Plugin~**](Plugin~) directory.
* See Unity’s documentation on [creating managed plugins](https://docs.unity3d.com/Manual/UsingDLL.html).
* You must download the [Blackmagic Desktop Video software](https://www.blackmagicdesign.com/developer/product/capture-and-playback), the latest version tested is **12.1**.
