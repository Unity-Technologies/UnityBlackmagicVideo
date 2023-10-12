#Bee bootstrap script. If you use powershell as your shell, runnning ./bee will directly run this script. If you're running cmd.exe
#running ./bee will run the bee.cmd and that will run powershell to run this script. This script is responsible for
#downloading a dotnet runtime if required, and for downloading a bee distribution if required, and for starting the actual bee
#driver executable.

trap
{
  # Ensure that we exit with an error code if there are uncaught exceptions.
  Write-Output $_
  exit 1
}

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression.FileSystem

#these two values will be replaced by the buildprocess when this script is used in a bootstrap distribution. When not replaced
#this script assumes it lives next to the standalone driver
$use_bee_from_steve="bee/386c0864f1b7_b81d4d429c20e87213262ecc6b0c22c3e0c49e753e9f41b5adc8d24d19edbe9a.zip"
$use_bee_from_steve_repo="https://public-stevedore.unity3d.com/r/public"

if("$use_bee_from_steve_repo" -Match "testing")
{
  Write-Host "Warning this bee bootstrap script is using a bee from the testing repository. This fine for testing, but not to use in production."
}  

#stevedore artifact for the dotnet runtimes. these are produced by the yamato CI for our "netcorerun" repo. The zip file contains
#an info file that describes from which git commit / yamato-id it was built.  They are plain upstream packages, just unzipped & rezipped
#with this extra information added.
$dotnet_runtime_win_x64 = "dotnet-runtime-win-x64/966cbdc_7c5a525a65145acdd639e31da9ec268a8be291950399308b922c5fbec230c3a5.zip" #net-runtime 6.0.3

$global:steve_artifact_return_value = ""
function Get-Stevedore-Artifact($steve_name, $steve_repo_url, $output_name)
{
    # We could extend this to parse the Stevedore.conf, and to attempt
    # downloads from multiple mirrors...
    $unzip_dir_path = "$HOME\.beebootstrap\$steve_name"
    
    if(![System.IO.Directory]::Exists($unzip_dir_path)) {
        
        $download_link = "$steve_repo_url/$steve_name"
        $random = Get-Random
        $temporary_dir = "$HOME/.beebootstrap/download_$random"
        New-Item -ItemType Directory -Force -Path $temporary_dir | Out-Null
        $downloaded_file = "$temporary_dir/download.zip"

        # Turn off this weird powershell progress thing. Not only is it super
        # ugly, it's also reported to be the cause of making the download
        # superslow.
        # https://stackoverflow.com/questions/28682642/powershell-why-is-using-invoke-webrequest-much-slower-than-a-browser-download
        $ProgressPreference = 'SilentlyContinue'
        
        Write-Host "Downloading $output_name"
        
        # Download into the temporary directory
        Invoke-WebRequest $download_link -outfile "$downloaded_file"

        Write-Host "Unzipping $output_name"
        
        # Extract into that same temporary directory
        [System.IO.Compression.ZipFile]::ExtractToDirectory("$downloaded_file", "$temporary_dir")

        # Make sure the parent directory of the target directory already exists
        $parent_dir = Split-Path -Parent $unzip_dir_path
        if( -Not (Test-Path -Path $parent_dir) )
        {
            New-Item -ItemType Directory -Path $parent_dir | Out-Null
        }
        
        # And we move the temporary directory to the final location. We use a
        # move, so that it's atomic, and a failed download, or a failed unzip
        # cannot result in a situation where the targetdirectory did get
        # created, but did not get properly populated. This way we can use
        # the presence of the directory as indication.
        Move-Item -Path "$temporary_dir" -Destination $unzip_dir_path

        # Remove the original zip file. Do this after moving because file
        # removal seems to be asynchronous and this would sometimes break the
        # directory move.
        Remove-Item "$unzip_dir_path/download.zip"
    }

    #we assign to a global instead of returning a value, since returning a value is somehow very brittle, as any command in this function that isn't piped to Out-Null
    #might actually pollute the return value
    $global:steve_artifact_return_value = $unzip_dir_path
}

if( $use_bee_from_steve -eq "no") {
    #this script supports running as part of a full bee distribution. In this case, use_bee_from_steve is not set,
    #and we find the path to the distribution by looking at where the script itself is. It shuold be placed in Standalone/Release of the distribution
    $standalone_release=$PSScriptRoot
}
else {
    #we also support downloading the bee distribution from a stevedore server. In this case use_bee_from_steve should be set to a stevedore artifact name.
    #we'll download it and run it. In this mode, the only thing a user needs to version in their repo is this script.
    Get-Stevedore-Artifact "$use_bee_from_steve" "$use_bee_from_steve_repo" "Bee distribution"
    $standalone_release = "$global:steve_artifact_return_value/Standalone/Release"
}

$distribution_path = [System.IO.Path]::GetFullPath("$standalone_release/../..")
$standalone_path = "$standalone_release/Bee.StandaloneDriver.exe"
if(![System.IO.File]::Exists($standalone_path)) {
    $standalone_path = "$standalone_release/Bee.StandaloneDriver.dll"
}

$dotnet_steve_artifact = "$dotnet_runtime_win_x64"
Get-Stevedore-Artifact $dotnet_steve_artifact "https://public-stevedore.unity3d.com/r/public" "Dotnet runtime"
$dotnet_exe = "$global:steve_artifact_return_value\dotnet.exe"

#we assign BEE_DOTNET_MUXER env var here, so that the bee that is running knows how it can use this dotnet runtime to start other net5 framework dependent apps.
#it uses this to run the stevedore downloader program on net5.
try {
    $env:BEE_DISTRIBUTION_PATH = $distribution_path
    $env:BEE_DOTNET_MUXER = $dotnet_exe

    # Run the wrapper and pass on any user args
    & $dotnet_exe $standalone_path $args
} finally {
    $env:BEE_DISTRIBUTION_PATH = $Null
    $env:BEE_DOTNET_MUXER = $Null
}

# Ensure exit code is preserved when running from another script.
exit $LastExitCode
# ReleaseNotes: 


# Automatically generated by Yamato Job: https://unity-ci.cds.internal.unity3d.com/job/12319484
#MANIFEST public: bee/386c0864f1b7_b81d4d429c20e87213262ecc6b0c22c3e0c49e753e9f41b5adc8d24d19edbe9a.zip