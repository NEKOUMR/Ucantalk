param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("win-x64", "win-x86", "win-arm64")]
    [string]$Runtime = "win-x64",
    [switch]$Sign = $true
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location (Split-Path -Parent $scriptDir)

if ($Sign) {
    powershell -ExecutionPolicy Bypass -File (Join-Path $scriptDir "build-msix.ps1") -Configuration $Configuration -Runtime $Runtime -Sign
}
else {
    powershell -ExecutionPolicy Bypass -File (Join-Path $scriptDir "build-msix.ps1") -Configuration $Configuration -Runtime $Runtime
}

powershell -ExecutionPolicy Bypass -File (Join-Path $scriptDir "install-msix.ps1")
