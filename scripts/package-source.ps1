[CmdletBinding()]
param(
    [string]$Output = (Join-Path $PSScriptRoot '..\artifacts\RocoModStudio-source.zip')
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputPath = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force -Path (Split-Path $outputPath) | Out-Null
$staging = Join-Path ([IO.Path]::GetTempPath()) ('roco-source-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $staging | Out-Null

$items = @('.github', '.editorconfig', '.gitattributes', '.gitignore', 'src', 'docs', 'scripts', 'tools\README.md', 'tools\manifest.example.json', 'README.md', 'LICENSE', 'CONTRIBUTING.md', 'CHANGELOG.md', 'SECURITY.md', 'global.json', 'RocoModStudio.slnx')
foreach ($item in $items) {
    $source = Join-Path $root $item
    if (Test-Path -LiteralPath $source) {
        $destination = Join-Path $staging $item
        New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination -Recurse -Force
    }
}
Get-ChildItem -LiteralPath $staging -Recurse -Directory -Force | Where-Object { $_.Name -in @('bin','obj','__pycache__') } | Sort-Object FullName -Descending | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Recurse -Force }
if (Test-Path -LiteralPath $outputPath) { Remove-Item -LiteralPath $outputPath -Force }
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $outputPath -CompressionLevel Optimal
Write-Host "Source archive: $outputPath"

