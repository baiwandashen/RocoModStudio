[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\artifacts')
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $root 'src\RocoModStudio\RocoModStudio.csproj'
$publish = Join-Path ([IO.Path]::GetFullPath($OutputRoot)) 'RocoModStudio'

Write-Host "Restoring $project"
dotnet restore $project
Write-Host "Building $project"
dotnet build $project -c $Configuration --no-restore
Write-Host "Publishing to $publish"
dotnet publish $project -c $Configuration -r $Runtime --self-contained false -o $publish --no-restore

$zip = Join-Path ([IO.Path]::GetFullPath($OutputRoot)) 'RocoModStudio-portable.zip'
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path $publish -DestinationPath $zip -CompressionLevel Optimal

Write-Host "Release directory: $publish"
Write-Host "Release archive:   $zip"
