[CmdletBinding()]
param(
    [string]$ToolsRoot = (Join-Path $PSScriptRoot '..\tools'),
    [string]$Manifest = (Join-Path $PSScriptRoot '..\tools\manifest.example.json'),
    [string]$FModelArchive,
    [string]$Cue4ParseArchive
)

$ErrorActionPreference = 'Stop'
$ToolsRoot = [IO.Path]::GetFullPath($ToolsRoot)
New-Item -ItemType Directory -Force -Path $ToolsRoot | Out-Null
$downloads = Join-Path $ToolsRoot '.downloads'
New-Item -ItemType Directory -Force -Path $downloads | Out-Null
$config = if (Test-Path -LiteralPath $Manifest) { Get-Content -Raw -LiteralPath $Manifest | ConvertFrom-Json } else { [pscustomobject]@{} }

function Get-Archive([string]$Source, [string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Source)) { return $null }
    if (Test-Path -LiteralPath $Source) { return (Resolve-Path -LiteralPath $Source).Path }
    $destination = Join-Path $downloads $Name
    Write-Host "Downloading $Name"
    Invoke-WebRequest -Uri $Source -OutFile $destination -UseBasicParsing
    return $destination
}

function Expand-Safe([string]$Archive, [string]$Destination) {
    $temp = Join-Path $downloads ("extract-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $temp | Out-Null
    Expand-Archive -LiteralPath $Archive -DestinationPath $temp -Force
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Get-ChildItem -LiteralPath $temp -Force | Copy-Item -Destination $Destination -Recurse -Force
}

function Copy-ExeWithDependencies([string]$Archive, [string]$ExeName, [string]$Destination) {
    if ([string]::IsNullOrWhiteSpace($Archive)) { Write-Warning "No archive supplied for $ExeName"; return }
    $temp = Join-Path $downloads ("extract-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $temp | Out-Null
    Expand-Archive -LiteralPath $Archive -DestinationPath $temp -Force
    $exe = Get-ChildItem -LiteralPath $temp -Recurse -File -Filter $ExeName | Select-Object -First 1
    if (-not $exe) { throw "$ExeName not found in $Archive" }
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Get-ChildItem -LiteralPath $exe.Directory.FullName -Force | Copy-Item -Destination $Destination -Recurse -Force
}

$repak = Get-Archive $config.repakZip 'repak.zip'
if ($repak) { Expand-Safe $repak (Join-Path $ToolsRoot 'repak') }

$dds = Get-Archive $config.ue4DdsZip 'ue4-dds-tools.zip'
if ($dds) { Expand-Safe $dds (Join-Path $ToolsRoot 'UE4-DDS-Tools') }

$node = Get-Archive $config.nodeZip 'node.zip'
if ($node) {
    $temp = Join-Path $downloads ("node-" + [Guid]::NewGuid().ToString('N'))
    Expand-Archive -LiteralPath $node -DestinationPath $temp -Force
    $nodeExe = Get-ChildItem -LiteralPath $temp -Recurse -File -Filter node.exe | Select-Object -First 1
    if ($nodeExe) {
        New-Item -ItemType Directory -Force -Path (Join-Path $ToolsRoot 'Node') | Out-Null
        Copy-Item -LiteralPath $nodeExe.FullName -Destination (Join-Path $ToolsRoot 'Node\node.exe') -Force
        $license = Get-ChildItem -LiteralPath $temp -Recurse -File -Filter LICENSE | Select-Object -First 1
        if ($license) { Copy-Item -LiteralPath $license.FullName -Destination (Join-Path $ToolsRoot 'Node\LICENSE.txt') -Force }
    }
}

$fmodel = Get-Archive ($(if ($FModelArchive) { $FModelArchive } else { $config.fmodelArchive })) 'fmodel.zip'
if ($fmodel) { Copy-ExeWithDependencies $fmodel 'FModel.exe' (Join-Path $ToolsRoot 'FModel') }

$cue = Get-Archive ($(if ($Cue4ParseArchive) { $Cue4ParseArchive } else { $config.cue4parseCliArchive })) 'cue4parse-cli.zip'
if ($cue) { Copy-ExeWithDependencies $cue 'cue4parse-cli.exe' (Join-Path $ToolsRoot 'CUE4ParseCli') }

Write-Host "Tools prepared at $ToolsRoot"
Get-ChildItem -LiteralPath $ToolsRoot -Directory | Select-Object Name, FullName
