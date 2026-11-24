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
# One readme for players: README.txt with the whole version history from CHANGELOG.md added at the end, built here
# every time so the two can never drift apart. (README.md and CHANGELOG.md are for the GitHub page only.)
function Format-Wrapped([string]$text, [string]$first, [string]$rest, [int]$width) {
    $lines = New-Object System.Collections.Generic.List[string]
    $line = $first
    foreach ($word in ($text -split '\s+' | Where-Object { $_ -ne '' })) {
        if ($line.Trim().Length -gt 0 -and ($line.Length + 1 + $word.Length) -gt $width) { $lines.Add($line.TrimEnd()); $line = $rest }
        if ($line -eq $first -or $line -eq $rest) { $line += $word } else { $line += ' ' + $word }
    }
    if ($line.Trim().Length -gt 0) { $lines.Add($line.TrimEnd()) }
    return $lines
}
$history = New-Object System.Collections.Generic.List[string]
$history.Add('VERSION HISTORY'); $history.Add('---------------')
$para = ''; $kind = 0 # 0 text, 1 bullet, 2 sub-bullet
function Push-Block { param([string]$p, [int]$k)
    if ($p.Trim().Length -eq 0) { return }
    $f = @('', ' - ', '    - ')[$k]; $r = @('', '   ', '      ')[$k]
    if ($k -eq 0 -and $history[$history.Count - 1] -ne '') { $history.Add('') } # a paragraph stands apart from the lists
    foreach ($l in (Format-Wrapped $p $f $r 78)) { $history.Add($l) }
    if ($k -eq 0) { $history.Add('') } }
foreach ($raw in (Get-Content (Join-Path $root 'CHANGELOG.md') -Encoding UTF8)) {
    if ($raw -match '^## Development milestones') { break }
    if ($raw -match '^# ') { continue }
    $t = $raw -replace '\*\*', '' -replace '`', ''
    if ($t -match '^## (.+)$') { Push-Block $para $kind; $para = ''; $kind = 0; $history.Add(''); $history.Add('  ' + $Matches[1].ToUpper()); continue }
    if ($t.Trim() -eq '') { Push-Block $para $kind; $para = ''; $kind = 0; continue }
    if ($t -match '^- (.+)$') { Push-Block $para $kind; $para = $Matches[1]; $kind = 1; continue }
    if ($t -match '^\s+- (.+)$') { Push-Block $para $kind; $para = $Matches[1]; $kind = 2; continue }
    $para = ($para + ' ' + $t.Trim()).Trim()
}
Push-Block $para $kind
for ($i = $history.Count - 1; $i -gt 0; $i--) { if ($history[$i] -eq '' -and $history[$i - 1] -eq '') { $history.RemoveAt($i) } }
$readme = (Get-Content (Join-Path $root 'README.txt') -Raw -Encoding UTF8).TrimEnd() + "`r`n`r`n" + ($history -join "`r`n") + "`r`n"
[System.IO.File]::WriteAllText((Join-Path $stage 'README.txt'), $readme, (New-Object System.Text.UTF8Encoding $false))

$zip = Join-Path $root "dist\FrontsAndRivals-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
# Zip by hand with forward-slash entry names (the zip standard). Windows PowerShell 5.1's Compress-Archive
# writes backslashes, which some unzip tools and mod managers read as part of the file name.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$stageFull = (Resolve-Path $stage).Path.TrimEnd('\') + '\'
$archive = [System.IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    Get-ChildItem $stage -Recurse -File | ForEach-Object {
        $name = $_.FullName.Substring($stageFull.Length).Replace('\', '/')
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $name, 'Optimal')
    }
} finally { $archive.Dispose() }
Remove-Item (Join-Path $root 'dist\stage') -Recurse -Force
Write-Host "Packaged $zip"
