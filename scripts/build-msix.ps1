param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("win-x64", "win-x86", "win-arm64")]
    [string]$Runtime = "win-x64",
    [switch]$Sign,
    [string]$CertSubject = "CN=User Name"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $projectRoot

$platform = if ($Runtime -eq "win-x86") { "x86" } elseif ($Runtime -eq "win-arm64") { "ARM64" } else { "x64" }
$publishProfile = "win-$platform.pubxml"

$args = @(
    "publish",
    "-c", $Configuration,
    "-r", $Runtime,
    "/p:Platform=$platform",
    "/p:PublishProfile=$publishProfile",
    "/p:WindowsPackageType=MSIX",
    "/p:GenerateAppxPackageOnBuild=true",
    "/p:AppxBundle=Never",
    "/p:UapAppxPackageBuildMode=SideloadOnly",
    "/p:AppxPackageDir=artifacts\msix\"
)

if ($Sign) {
    $packagingDir = Join-Path $projectRoot "packaging"
    New-Item -ItemType Directory -Force -Path $packagingDir | Out-Null
    $msixOutDir = Join-Path $projectRoot "artifacts\msix"
    New-Item -ItemType Directory -Force -Path $msixOutDir | Out-Null

    $cerPath = Join-Path $packagingDir "Ucantalk_TempCert.cer"
    $cerOutPath = Join-Path $msixOutDir "Ucantalk_TempCert.cer"

    $cert = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $CertSubject } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

    if ($null -eq $cert) {
        $cert = New-SelfSignedCertificate `
            -Type CodeSigningCert `
            -Subject $CertSubject `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -NotAfter (Get-Date).AddYears(5)
    }

    Export-Certificate -Cert $cert -FilePath $cerPath -Force | Out-Null
    Copy-Item -Force $cerPath $cerOutPath

    $args += "/p:AppxPackageSigningEnabled=true"
    $args += "/p:PackageCertificateThumbprint=$($cert.Thumbprint)"
}
else {
    $args += "/p:AppxPackageSigningEnabled=false"
}

Write-Host "dotnet $($args -join ' ')"
dotnet @args
