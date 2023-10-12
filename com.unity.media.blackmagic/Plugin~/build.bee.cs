using Bee.Core;
using Bee.Toolchain.Xcode;
using Bee.Toolchain.VisualStudio;
using NiceIO;
using System;
using System.Collections.Generic;
using System.Linq;
using Bee.NativeProgramSupport;

using var _ = new BuildProgramContext();

var np = new NativeProgram("Blackmagic")
{
    OutputName = { c => $"Blackmagic{(c.CodeGen == CodeGen.Debug ? "_d" : "")}" },
    Sources =
    {
        "Blackmagic.cpp",
        "Sources",
        {IsOSX(), "external/blackmagic/osx", "platform/osx"},
        {IsWin(), "external/blackmagic/win", "platform/win"},
        {IsLinux(), "external/blackmagic/linux", "platform/linux"},
    },
    IncludeDirectories = {
        "external/Unity",
        "Includes",
        {IsWin(), "external/NVIDIA_GPUDirect/include", "platform/win", IdlOutputPath(), "external/blackmagic/win/include"},
        {IsOSX(), "platform/osx", "external/blackmagic/osx/include"},
        {IsLinux(), "platform/linux", "external/blackmagic/linux/include"},
    },
    Libraries = {
        {IsOSX(),  new SystemFramework("CoreFoundation")},
        {IsWin(), new StaticLibrary("external/NVIDIA_GPUDirect/lib/x64/dvp.lib")},
        {IsLinux(), new SystemLibrary("dl")}
    }
};

var windowsToolchain = ToolChain.Store.Windows().VS2019().Sdk_18362().x64();
var linuxToolchain = ToolChain.Store.Linux().Centos_7_4().Clang_5_0_1().x64();
var macX64ToolChain = ToolChain.Store.Mac().Sdk_11_1().x64();
var macArm64ToolChain = ToolChain.Store.Mac().Sdk_11_1().ARM64();

if (windowsToolchain.CanBuild)
{
    var (bmInterfaceHeader, bmInterfaceSource) = new IdlCompiler(windowsToolchain.Sdk).SetupInvocation(IdlOutputPath(), $"external/blackmagic/win/include/DeckLinkAPI.idl");
    np.Sources.Add(IsWin(), bmInterfaceSource);
    np.ExtraDependenciesForAllObjectFiles.Add(IsWin(), bmInterfaceHeader);
}

foreach (var codegen in new[] { CodeGen.Debug, CodeGen.Release })
{
    if (windowsToolchain.CanBuild)
        SetupAndDeploy(windowsToolchain, $"../Runtime/Plugin/win64", codegen);
    if (linuxToolchain.CanBuild)
        SetupAndDeploy(linuxToolchain, $"../Runtime/Plugin/linux64", codegen);

    if (macX64ToolChain.CanBuild)
    {
        var x64 = SetupSpecificConfiguration(macX64ToolChain, codegen);
        var arm64 = SetupSpecificConfiguration(macArm64ToolChain, codegen);
        var lipoResult = Lipo.Setup((XcodeSdk)macArm64ToolChain.Sdk, new[] { x64.Path, arm64.Path });
        Backend.Current.SetupCopyFile($"../Runtime/Plugin/osx/{lipoResult.FileName}", lipoResult);
    }
}

BuiltNativeProgram SetupSpecificConfiguration(ToolChain toolChain, CodeGen codeGen1) =>
    np.SetupSpecificConfiguration(
        new NativeProgramConfiguration(codeGen1, toolChain, lump: true),
        toolChain.DynamicLibraryFormat
    );

BuiltNativeProgram SetupAndDeploy(ToolChain toolChain, NPath deployDir, CodeGen codeGen) => SetupSpecificConfiguration(toolChain, codeGen).DeployTo(deployDir);

Func<NativeProgramConfiguration, bool> IsOSX() => config => config.Platform is MacOSXPlatform;
Func<NativeProgramConfiguration, bool> IsWin() => config => config.Platform is WindowsPlatform;
Func<NativeProgramConfiguration, bool> IsLinux() => config => config.Platform is LinuxPlatform;

NPath IdlOutputPath() => Backend.Current.ArtifactsPath.Combine("blackmagic_gen");
public class IdlCompiler
{
    private VisualStudioSdk Sdk { get; }

    public IdlCompiler(VisualStudioSdk sdk) => Sdk = sdk;

    public (NPath headerFilePath, NPath cFilePath) SetupInvocation(NPath outputFolder, NPath idlFilePath)
    {
        var headerFileName = idlFilePath.ChangeExtension(".h").FileName;
        var headerFilePath = outputFolder.Combine(headerFileName);
        var cFileName = idlFilePath.ChangeExtension(".c").FileName;
        var cFilePath = outputFolder.Combine(cFileName);
        var midlExe = Sdk.ToolPath("midl.exe");

        Backend.Current.AddAction(
            "VisualStudio_midl",
            new[] { headerFilePath, cFilePath },
            new[] { idlFilePath, midlExe },
            midlExe.InQuotesResolved(SlashMode.Native),
            GetMidlArgs(outputFolder, idlFilePath, headerFileName, cFileName).ToArray(),
            environmentVariables: Sdk.EnvironmentVariables
        );
        return (headerFilePath, cFilePath);
    }

    IEnumerable<string> GetMidlArgs(NPath outputDir, NPath idlFile, NPath headerFile, NPath iidFile)
    {
        yield return "/env";
        yield return "x64";
        yield return "/h";
        yield return headerFile.ToString();
        yield return "/iid";
        yield return iidFile.ToString();
        yield return "/out";
        yield return outputDir.MakeAbsolute().ToString(SlashMode.Native);
        yield return "/W1";
        yield return "/char";
        yield return "signed";
        yield return "/notlb";
        yield return "/target";
        yield return "NT60";
        foreach (var iPath in Sdk.IncludePaths)
        {
            yield return "/I";
            yield return iPath.ResolveWithFileSystem().InQuotes(SlashMode.Native);
        }
        yield return "/nologo";
        yield return idlFile.MakeAbsolute().ToString(SlashMode.Native);
    }
}
