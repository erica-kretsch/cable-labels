<#
.SYNOPSIS
Builds the mod with Unity, headless, and installs it into LaunchPad mods folders.

.DESCRIPTION
Runs CableLabels.Editor.Export.Run in batch mode, which exports the mod as the editor's
Build button would, into Build/CableLabels/. Then replaces <target>\CableLabels in each
install target with the DLL, About/, GameData/ and asset bundles from that export.

Batch mode can't open a project the editor already has open. Then close the editor, or
build from it (File > Build Settings > Build, to Build/CableLabels.exe) and pass -SkipBuild.

.PARAMETER Target
Server: E:\Stationeers\mods, the dedicated server owned by coding agents.
Local: the Documents\My Games\Stationeers\mods folder of the current user.
All (the default): both.

.PARAMETER SkipBuild
Install the last export in Build/CableLabels/ without running Unity.

.PARAMETER UnityEditor
Unity.exe to build with. Defaults to Unity Hub's install of the version in
ProjectSettings/ProjectVersion.txt.
#>
[CmdletBinding()]
param(
    [ValidateSet('All', 'Server', 'Local')]
    [string]$Target = 'All',
    [switch]$SkipBuild,
    [string]$UnityEditor
)

$ErrorActionPreference = 'Stop'
$ProjectDir = Split-Path -Parent $PSScriptRoot

# LaunchPadExport.Sanitize(PlayerSettings.productName); the exporter names its folder, the
# DLL's asmdef and the bundles after it.
$ModName = 'CableLabels'
$ExportDir = Join-Path $ProjectDir "Build\$ModName"
$LogFile = Join-Path $ProjectDir 'Logs\install-export.log'

$Targets = [ordered]@{
    Server = 'E:\Stationeers\mods'
    Local  = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'My Games\Stationeers\mods'
}
if ($Target -ne 'All') { $Targets = [ordered]@{ $Target = $Targets[$Target] } }

function Test-EditorOpen {
    # Unity holds Temp/UnityLockfile open while the project is open. A leftover file from a
    # crash isn't locked, so try to open it.
    $lock = Join-Path $ProjectDir 'Temp\UnityLockfile'
    if (-not (Test-Path $lock)) { return $false }
    try {
        [IO.File]::Open($lock, 'Open', 'ReadWrite', 'None').Dispose()
        return $false
    } catch {
        return $true
    }
}

if (-not $SkipBuild) {
    if (Test-EditorOpen) {
        throw ("The Unity editor has this project open, and batch mode can't open it too. " +
            "Close the editor, or build from it to Build/$ModName.exe and pass -SkipBuild.")
    }

    if (-not $UnityEditor) {
        $versionLine = Get-Content (Join-Path $ProjectDir 'ProjectSettings\ProjectVersion.txt') |
            Where-Object { $_ -match '^m_EditorVersion:\s*(\S+)' } | Select-Object -First 1
        $version = [regex]::Match($versionLine, '^m_EditorVersion:\s*(\S+)').Groups[1].Value
        $UnityEditor = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
    }
    if (-not (Test-Path $UnityEditor)) {
        throw "Unity not found at '$UnityEditor'. Pass -UnityEditor, or -SkipBuild."
    }

    Write-Host "Exporting with Unity (log: $LogFile)"
    New-Item -ItemType Directory -Force (Split-Path $LogFile) | Out-Null
    $unityArgs = @(
        '-batchmode', '-quit', '-nographics',
        '-projectPath', "`"$ProjectDir`"",
        '-executeMethod', 'CableLabels.Editor.Export.Run',
        '-logFile', "`"$LogFile`""
    )
    $unity = Start-Process -FilePath $UnityEditor -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
    if ($unity.ExitCode -ne 0) {
        Get-Content $LogFile -Tail 40 | Write-Host
        throw "Unity exited with code $($unity.ExitCode); see $LogFile."
    }
}

$dll = Join-Path $ExportDir "$ModName.dll"
if (-not (Test-Path $dll)) { throw "No export at $ExportDir ($ModName.dll is missing)." }

# What LaunchPad loads: our DLL and its .pdb if Unity wrote one, About/, GameData/, and the
# bundles. The exporter leaves bundles in StandaloneWindows/ with Unity's manifests beside
# them; only the .assets files are installed, at the mod's root.
$files = @(Get-ChildItem $ExportDir -File | Where-Object { $_.Extension -in '.dll', '.pdb' })
$bundles = @()
$bundleDir = Join-Path $ExportDir 'StandaloneWindows'
if (Test-Path $bundleDir) { $bundles = @(Get-ChildItem $bundleDir -File -Filter '*.assets') }
$folders = @('About', 'GameData') | ForEach-Object { Join-Path $ExportDir $_ } |
    Where-Object { Test-Path $_ }

foreach ($name in $Targets.Keys) {
    $root = $Targets[$name]
    if (-not (Test-Path $root)) { throw "$name mods folder not found: $root" }
    $dest = Join-Path $root $ModName

    # Replace the whole folder, so files dropped from the mod don't linger.
    if (Test-Path $dest) {
        try {
            Remove-Item $dest -Recurse -Force
        } catch {
            throw "Couldn't replace $dest; is the game or server running? $($_.Exception.Message)"
        }
    }
    New-Item -ItemType Directory $dest | Out-Null
    $files + $bundles | Copy-Item -Destination $dest
    $folders | Copy-Item -Destination $dest -Recurse

    Write-Host "Installed to $dest ($name)"
}
