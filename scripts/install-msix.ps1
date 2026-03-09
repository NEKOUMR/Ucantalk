param(
    [string]$MsixPath = "",
    [switch]$TrustCert = $true
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $projectRoot
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).
    IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if ([string]::IsNullOrWhiteSpace($MsixPath)) {
    $msix = Get-ChildItem "artifacts\msix" -Recurse -Filter *.msix |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $msix) {
        throw "No MSIX found. Run scripts\\build-msix.ps1 first."
    }

    $MsixPath = $msix.FullName
}

if (-not (Test-Path $MsixPath)) {
    throw "MSIX file not found: $MsixPath"
}

if ($TrustCert) {
    $certPath = Join-Path $projectRoot "artifacts\msix\Ucantalk_TempCert.cer"
    if (Test-Path $certPath) {
        if ($isAdmin) {
            certutil -f -addstore Root $certPath | Out-Null
            certutil -f -addstore TrustedPeople $certPath | Out-Null
        }
        else {
            certutil -user -f -addstore Root $certPath | Out-Null
            certutil -user -f -addstore TrustedPeople $certPath | Out-Null
            Write-Warning "Not running as Administrator. If install fails with certificate trust error, rerun PowerShell as Administrator."
        }
    }
}

Add-AppxPackage -Path $MsixPath
Write-Host "Install succeeded: $MsixPath"
