# Blackmagic Video support with the Unity Editor

Use the Unity Blackmagic package (`com.unity.media.blackmagic`) to use video capture cards from Blackmagic Design to input and output pro-grade video and audio in the Unity Editor and runtime builds.

This repository contains the code package, the test package and test projects related with Unity Blackmagic Video.

## Get started

To learn about the Unity Blackmagic Video package (concepts, features, and workflows) read the [Blackmagic package documentation](com.unity.media.blackmagic/Documentation~/index.md) in this repository.  
For user convenience, an HTML build is also available [in a zip file](com.unity.media.blackmagic/Built-documentation.zip).

### Requirements

* Windows 10 or macOS
* A Blackmagic Design capture card
* A Camera with SDI or HDMI output (depending on capture card)

### Check out the licensing model

The Blackmagic Video package is licensed under the [under the Apache License, Version 2.0](LICENSE.md).

### Contribution and maintenance

We appreciate your interest in contributing to the Unity Blackmagic Video package.  
It's important to note that **this package is provided as is, without any maintenance or release plan.**  
Therefore, we are unable to monitor bug reports, accept feature requests, or review pull requests for this package.

However, we understand that users may want to make improvements to the package.  
In that case, we recommend that you fork the repository. This will allow you to make changes and enhancements as you see fit.

## Blackmagic package

### Access the Blackmagic package folder

| Package | Description                                                                                        |
| :--- |:---------------------------------------------------------------------------------------------------|
| **[Blackmagic Video](com.unity.media.blackmagic)**| The package that allows you to use video capture cards from Blackmagic Design in the Unity Editor. |

### Test the Blackmagic package

Use this Unity project to run various tests against the Blackmagic package.

| Project                        | Description                               |
|--------------------------------|-------------------------------------------|
| [**TestProject**](TestProject) | Used to run the Blackmagic package tests. |

| Project                                                                       | Description                                                    |
|:------------------------------------------------------------------------------|:---------------------------------------------------------------|
| [**Blackmagic_HDRP_Compositing**](SampleProjects/Blackmagic_HDRP_Compositing) | Blackmagic Video I/O with HDRP Compositor Sample.              |
| [**Blackmagic_Legacy**](SampleProjects/Blackmagic_Legacy)                     | Blackmagic and Legacy Render Pipeline Sample Project.          |
| [**Blackmagic_URP**](SampleProjects/Blackmagic_URP)                           | Blackmagic and Universal Render Pipeline (URP) Sample Project. |
| [**Blackmagic_Synchronization**](SampleProjects/Blackmagic_Synchronization)   | Blackmagic Synchronization Sample Project.                     |

## Building the plugin

The package uses a compiled assembly that provides access to Blackmagic's API. The files needed to build the plugin are in the **Plugin~** directory. See Unity’s documentation on [creating managed plugins](https://docs.unity3d.com/Manual/UsingDLL.html).
You must download the [Blackmagic Desktop Video software](https://www.blackmagicdesign.com/developer/product/capture-and-playback), the latest version tested is **12.1**.
