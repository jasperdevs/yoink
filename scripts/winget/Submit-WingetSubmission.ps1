param(
    [Parameter(Mandatory = $true)]
    [string]$ManifestDirectory,

    [Parameter(Mandatory = $true)]
    [string]$GitHubToken,

    [string]$WingetCreatePath = (Join-Path $env:TEMP 'wingetcreate.exe')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $WingetCreatePath)) {
    Invoke-WebRequest 'https://aka.ms/wingetcreate/latest' -OutFile $WingetCreatePath
}

& $WingetCreatePath submit --token $GitHubToken $ManifestDirectory
