<#
.SYNOPSIS
    Builds and deploys (registers) the WhatSprintIsItWidget on the local machine.

.DESCRIPTION
    Builds the packaged MSIX project with MSBuild and registers it with the
    Windows Widgets host using the loose AppX layout (Add-AppxPackage -Register).
    Registering the loose layout is the fast dev-inner-loop approach and does
    not require code-signing.

    After a successful run, open the Widgets board (Win + W) -> Add widgets ->
    "Azure DevOps Sprint".

.PARAMETER Configuration
    Build configuration: Release (default) or Debug.

.PARAMETER Platform
    Target platform: x64, arm64, or x86. Defaults to the current OS architecture.

.PARAMETER Package
    Build a sideloadable MSIX package and install it with Add-AppxPackage
    instead of registering the loose layout. Requires a signing certificate
    (a self-signed test cert is generated if none is configured).

.PARAMETER Unregister
    Remove a previously registered/installed WhatSprintIsItWidget, then exit.

.EXAMPLE
    .\build-deploy.ps1

.EXAMPLE
    .\build-deploy.ps1 -Configuration Debug -Platform arm64

.EXAMPLE
    .\build-deploy.ps1 -Unregister
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    [ValidateSet('x64', 'arm64', 'x86')]
    [string]$Platform,

    [switch]$Package,

    [switch]$Unregister
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$PackageName = 'Contoso.WhatSprintIsItWidget'
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Join-Path $ScriptRoot 'src'
$ProjectFile = Join-Path $ProjectDir 'WhatSprintIsItWidget.csproj'

function Write-Step([string]$Message) {
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Resolve-Platform {
    if ($Platform) { return $Platform }
    switch ($env:PROCESSOR_ARCHITECTURE) {
        'AMD64' { return 'x64' }
        'ARM64' { return 'arm64' }
        'x86'   { return 'x86' }
        default { return 'x64' }
    }
}

function Find-MSBuild {
    # Prefer full MSBuild (required for MSIX packaging targets).
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $path = & $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild `
            -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if ($path -and (Test-Path $path)) { return $path }
    }

    $cmd = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    throw 'MSBuild.exe not found. Install Visual Studio 2022 with the ' +
          '".NET desktop development" and "Windows application development" workloads.'
}

function Assert-Prerequisites {
    $build = [int](Get-CimInstance Win32_OperatingSystem).BuildNumber
    if ($build -lt 22000) {
        throw "Windows 11 (build 22000+) is required for widgets. Detected build $build."
    }

    $devKey = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock'
    $devMode = (Get-ItemProperty -Path $devKey -Name AllowDevelopmentWithoutDevLicense -ErrorAction SilentlyContinue).AllowDevelopmentWithoutDevLicense
    if ($devMode -ne 1) {
        Write-Warning ('Developer Mode does not appear to be enabled. If registration ' +
            'fails, enable it: Settings -> Privacy and security -> For developers -> Developer Mode.')
    }
}

function Invoke-Unregister {
    Write-Step "Removing existing '$PackageName' registrations"
    $pkgs = Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue
    if (-not $pkgs) {
        Write-Host 'Nothing to remove.' -ForegroundColor Yellow
        return
    }
    foreach ($p in $pkgs) {
        Write-Host "Removing $($p.PackageFullName)"
        Remove-AppxPackage -Package $p.PackageFullName
    }
    Write-Host 'Removed.' -ForegroundColor Green
}

# --- main -------------------------------------------------------------------

if (-not (Test-Path $ProjectFile)) {
    throw "Project not found: $ProjectFile"
}

if ($Unregister) {
    Invoke-Unregister
    return
}

Assert-Prerequisites
$plat = Resolve-Platform
$msbuild = Find-MSBuild

Write-Host "Project      : $ProjectFile"
Write-Host "Configuration: $Configuration"
Write-Host "Platform     : $plat"
Write-Host "MSBuild      : $msbuild"

$commonArgs = @(
    $ProjectFile,
    '/nologo',
    '/restore',
    "/p:Configuration=$Configuration",
    "/p:Platform=$plat",
    '/v:minimal'
)

if ($Package) {
    Write-Step 'Building sideloadable MSIX package'
    $packageArgs = $commonArgs + @(
        '/p:UapAppxPackageBuildMode=SideloadOnly',
        '/p:AppxBundle=Never',
        '/p:GenerateAppxPackageOnBuild=true',
        '/p:AppxPackageSigningEnabled=true',
        '/p:AppxAutoIncrementPackageRevision=false'
    )
    & $msbuild @packageArgs
    if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE." }

    Write-Step 'Locating and installing the MSIX'
    $msix = Get-ChildItem -Path $ProjectDir -Recurse -Filter '*.msix' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $msix) {
        throw 'No .msix produced. Check the build output above.'
    }
    Write-Host "Installing $($msix.FullName)"
    Add-AppxPackage -Path $msix.FullName -ForceUpdateFromAnyVersion
}
else {
    Write-Step 'Building AppX layout (loose files)'
    # Produce the unpacked AppX layout without generating a signed package.
    $layoutArgs = $commonArgs + @(
        '/p:GenerateAppxPackageOnBuild=false',
        '/p:AppxPackageSigningEnabled=false'
    )
    & $msbuild @layoutArgs
    if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE." }

    Write-Step 'Registering the loose AppX layout'
    $manifest = Get-ChildItem -Path $ProjectDir -Recurse -Filter 'AppxManifest.xml' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\bin\\" -and $_.FullName -match [regex]::Escape($Configuration) } |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $manifest) {
        throw 'AppxManifest.xml not found in the build output. Try -Package instead.'
    }
    Write-Host "Registering $($manifest.FullName)"
    Add-AppxPackage -Register $manifest.FullName -ForceUpdateFromAnyVersion
}

Write-Step 'Done'
$installed = Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue
if ($installed) {
    Write-Host "Installed: $($installed.PackageFullName)" -ForegroundColor Green
}
Write-Host ''
Write-Host 'Next: press Win + W -> Add widgets -> "Azure DevOps Sprint".' -ForegroundColor Green
