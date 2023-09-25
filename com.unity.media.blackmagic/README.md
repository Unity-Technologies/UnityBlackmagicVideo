# Blackmagic Video

The Blackmagic Video package helps you leverage video capture cards from Blackmagic Design to input and output pro-grade video and audio in the Unity Editor and runtime builds, unlocking new workflows for users working in film, virtual production, live events, and broadcast.

## Installation and use

To get instructions about installing and using the Blackmagic Video package in the Unity Editor, download the [`Built-documentation.zip`](Built-documentation.zip) file available in this folder, unzip it locally, and open the `index.html` file.

>**Note:** The `Documentation~` folder includes the documentation sources in Markdown.

## Requirements

* Windows 10 or macOS
* Blackmagic capture card
* Camera with SDI or HDMI output (depending on capture card)

## Building the plugin

The package uses a compiled assembly that provides access to Blackmagic's API. The files needed to build the plugin are in the **Plugin~** directory. See Unity’s documentation on [creating managed plugins](https://docs.unity3d.com/Manual/UsingDLL.html).
You must download the [Blackmagic Desktop Video software](https://www.blackmagicdesign.com/developer/product/capture-and-playback), the latest version tested is **12.1**.
