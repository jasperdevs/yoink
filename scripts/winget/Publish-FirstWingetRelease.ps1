param(
    [Parameter(Mandatory = $true)]
    [string]$PackageVersion,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true)]
    [string]$GitHubToken,

    [Parameter(Mandatory = $true)]
    [string]$X64InstallerUrl,
    [Parameter(Mandatory = $true)]
    [string]$X64InstallerSha256,
    [Parameter(Mandatory = $true)]
    [string]$X86InstallerUrl,
    [Parameter(Mandatory = $true)]
    [string]$X86InstallerSha256,
    [Parameter(Mandatory = $true)]
    [string]$Arm64InstallerUrl,
    [Parameter(Mandatory = $true)]
    [string]$Arm64InstallerSha256
)

$ErrorActionPreference = 'Stop'

$manifestRoot = Join-Path $OutputDirectory 'winget'

& (Join-Path $PSScriptRoot 'New-WingetManifests.ps1') `
    -PackageVersion $PackageVersion `
    -OutputDirectory $manifestRoot `
    -X64InstallerUrl $X64InstallerUrl `
    -X64InstallerSha256 $X64InstallerSha256 `
    -X86InstallerUrl $X86InstallerUrl `
    -X86InstallerSha256 $X86InstallerSha256 `
    -Arm64InstallerUrl $Arm64InstallerUrl `
    -Arm64InstallerSha256 $Arm64InstallerSha256

winget validate $manifestRoot

& (Join-Path $PSScriptRoot 'Submit-WingetSubmission.ps1') `
    -ManifestDirectory (Join-Path $manifestRoot $PackageVersion) `
    -GitHubToken $GitHubToken
