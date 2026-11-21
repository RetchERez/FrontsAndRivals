# Builds the Fronts & Rivals release zip in dist\. Contains only this mod's own files.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'src'
& dotnet build (Join-Path $src 'FrontsAndRivals.csproj') -c Release -p:GtaVDir= | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

$version = ([xml](Get-Content (Join-Path $src 'FrontsAndRivals.csproj'))).Project.PropertyGroup.Version | Select-Object -First 1
$bin = Join-Path $src 'bin\Release\net48'
$stage = Join-Path $root "dist\stage\FrontsAndRivals-$version"
if (Test-Path (Join-Path $root 'dist\stage')) { Remove-Item (Join-Path $root 'dist\stage') -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $stage 'scripts\FrontsAndRivals') | Out-Null

Copy-Item (Join-Path $bin 'FrontsAndRivals.dll') (Join-Path $stage 'scripts')
Copy-Item (Join-Path $src 'FrontsAndRivals.ini') (Join-Path $stage 'scripts')
Copy-Item (Join-Path $src 'Content\*.json') (Join-Path $stage 'scripts\FrontsAndRivals')
Copy-Item (Join-Path $root 'README.md') (Join-Path $stage 'README.md')
Copy-Item (Join-Path $root 'README.txt') (Join-Path $stage 'README.txt')
Copy-Item (Join-Path $root 'CHANGELOG.md') (Join-Path $stage 'CHANGELOG.md')

$zip = Join-Path $root "dist\FrontsAndRivals-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Remove-Item (Join-Path $root 'dist\stage') -Recurse -Force
Write-Host "Packaged $zip"
