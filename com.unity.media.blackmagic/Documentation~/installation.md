[Contents](TableOfContents.md) | [Home](index.md) > Installation

# Installation

This package is only available in its repository, and can't be discovered via the Unity registry.

To install it, you have two main options:

* Get a local copy of the package (i.e. the folder that includes the ­­`package.json` file) and follow the instructions for [local package installation](https://docs.unity3d.com/Manual/upm-ui-local.html), OR

* Install the package directly from its GitHub repository [using a Git URL](https://docs.unity3d.com/Manual/upm-ui-giturl.html).

# Requirements

## Unity / system

|Item |Description |
|--|--|
| **Unity Editor**| **2022.2 or later** (recommended).|
| **Render pipeline**| **Legacy**, **URP**, or **HDRP**.|
|**OS** | **Windows 10** or **macOS**.|

## Video

| Item| Description|
|--|--|
| **Video card**| A Blackmagic video capture card.<br /><br /> Three cards have been tested: the `DeckLink 8K Pro`, the `DeckLink Duo 2`, and the `DeckLink 4K Extreme 12G`.|
| **Camera**| A **camera** capable of outputting a video signal to the capture card via **SDI**.|
| **Video software**|[Blackmagic Desktop Video software](https://www.blackmagicdesign.com/developer/product/capture-and-playback) versions `12.0.0` and above. <br /><br />When opening the **Blackmagic Desktop Video** software, your DeckLink card must appear in the list of the products found. If not, your DeckLink card is probably not correctly installed on your computer's PCI port.|

## Optional hardware

* An **SDI monitor** to output a final composited frame.
* A **Sync Generator** to use the Genlock feature of the Blackmagic video capture card.
* Multiple **Blackmagic video capture cards** at the same time.


# Known limitations

 You can use the [Graphics Compositor](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@10.2/manual/Compositor-User-Guide.html) tool to do compositing between the Blackmagic Input Video and the Unity scene. This tool is:

* Not mandatory.
* Only available on **Unity 2020.1** or later and **HDRP 9+**.
* Recommended to be used with **HDRP 11+**.

Using a multi-card setup requires you to rename your Logical Devices in the `Label` field using the [Blackmagic Desktop Video](https://www.blackmagicdesign.com/developer/product/capture-and-playback) Setup. The Device `Label` field is used to select a different Connector mapping per card.
