param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64",
    [string]$Version = "",
    [string]$IsccPath = ""
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $projectRoot

$prereqDir = Join-Path $projectRoot "packaging\\prereqs"
$vcRedist = Join-Path $prereqDir "vc_redist.x64.exe"
$desktopRuntime = Join-Path $prereqDir "windowsdesktop-runtime-8-x64.exe"
if (-not (Test-Path $vcRedist)) {
    New-Item -ItemType Directory -Path $prereqDir -Force | Out-Null
    Write-Host "Downloading VC++ runtime..."
    Invoke-WebRequest -Uri "https://aka.ms/vs/17/release/vc_redist.x64.exe" -OutFile $vcRedist
}

if (-not (Test-Path $desktopRuntime)) {
    New-Item -ItemType Directory -Path $prereqDir -Force | Out-Null
    Write-Host "Downloading .NET Desktop Runtime 8 x64..."
    Invoke-WebRequest -Uri "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe" -OutFile $desktopRuntime
}

dotnet publish -c $Configuration -r $Runtime /p:Platform=x64 /p:PublishProfile=win-x64.pubxml
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

function Resolve-IsccPath {
    param([string]$CandidatePath)

    if (-not [string]::IsNullOrWhiteSpace($CandidatePath)) {
        if (Test-Path $CandidatePath) {
            return $CandidatePath
        }
        throw "ISCC not found: $CandidatePath"
    }

    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($null -ne $command -and (Test-Path $command.Source)) {
        return $command.Source
    }

    $knownPaths = @(
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\\ISCC.exe"),
        (Join-Path $env:LocalAppData "Programs\\Inno Setup 6\\ISCC.exe")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($knownPath in $knownPaths) {
        if (Test-Path $knownPath) {
            return $knownPath
        }
    }

    throw "ISCC.exe not found. Install Inno Setup 6 or pass -IsccPath <path>."
}

$iscc = Resolve-IsccPath -CandidatePath $IsccPath

$iss = Join-Path $projectRoot "packaging\inno\VRC_cantalkcn.iss"
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = "1.1.0"
}

Write-Host "Building installer version: $Version"
& $iscc "/DMyAppVersion=$Version" $iss
if ($LASTEXITCODE -ne 0) {
    throw "ISCC failed with exit code $LASTEXITCODE"
}

$setup = Get-ChildItem "artifacts\installer" -Filter *.exe |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $setup) {
    throw "Setup EXE was not generated."
}

Write-Host "Setup generated: $($setup.FullName)"
