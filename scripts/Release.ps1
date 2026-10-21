<#
.SYNOPSIS
    Cuts a whole release in one go: version bump, three builds, notes, publish, manifest.

.DESCRIPTION
    Bumps the version in Directory.Build.props, renames the Unreleased changelog heading to it,
    commits and pushes (the pre-push hook runs the whole suite once), builds x64, ARM64 and
    32 bit without testing again, writes the release notes from the changelog with the hashes
    filled in, publishes x64 last so the manifest's final write names every architecture, and
    pulls the manifest commit back.

    -DryRun stops after the builds and the notes, with nothing committed or published.

.EXAMPLE
    .\Release.ps1

.EXAMPLE
    .\Release.ps1 -Bump patch -Headline 'A fix for the line list.'

.EXAMPLE
    .\Release.ps1 -DryRun
#>
[CmdletBinding()]
param(
    [ValidateSet('major', 'minor', 'patch', 'none')]
    [string]$Bump = 'minor',

    [string]$Headline,

    [string]$NotesFile,

    [switch]$DryRun,

    [string]$CertThumbprint,

    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'

# A dry run builds what the props already say, so its files carry the current version.
if ($DryRun) { $Bump = 'none' }

$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root 'dist'
$propsPath = Join-Path $root 'Directory.Build.props'
$changelogPath = Join-Path $root 'distribution\CHANGELOG.md'
$publish = Join-Path $PSScriptRoot 'Publish.ps1'
$runtimes = @('win-arm64', 'win-x86', 'win-x64')
$repo = 'jsphgei-dot/tts-util-win'

function Invoke-Git { git -C $root @args; if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed." } }

function Read-Text([string]$path) { [IO.File]::ReadAllText($path) }

function Write-Text([string]$path, [string]$text) { [IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($true)) }

# Checks, all before anything changes

if (-not $DryRun) {
    gh auth status *> $null
    if ($LASTEXITCODE -ne 0) { throw 'gh is not signed in. Run gh auth login first.' }

    $branch = (git -C $root branch --show-current).Trim()
    if ($branch -ne 'master') { throw "Releases are cut from master, and this is $branch." }

    if (git -C $root status --porcelain) { throw 'The working tree has changes. Commit or stash them first.' }

    Invoke-Git fetch --quiet origin
    if ((git -C $root rev-list --count HEAD..origin/master) -ne '0') { throw 'origin/master has commits this clone lacks. Pull first.' }
}

$changelog = Read-Text $changelogPath
$heading = if ($DryRun) { '## [^\r\n]+' } else { '## Unreleased' }
$unreleased = [regex]::Match($changelog, "(?ms)^$heading\r?\n(.*?)(?=^## )")
if (-not $unreleased.Success -or -not $unreleased.Groups[1].Value.Trim()) {
    throw 'The changelog has no Unreleased entries, so there is nothing to release.'
}

# Version

$props = Read-Text $propsPath
$prefix = [regex]::Match($props, '<VersionPrefix>([^<]+)</VersionPrefix>').Groups[1].Value
$suffix = [regex]::Match($props, '<VersionSuffix>([^<]*)</VersionSuffix>').Groups[1].Value
$code = [int][regex]::Match($props, '<VersionCode>([^<]+)</VersionCode>').Groups[1].Value

$parts = $prefix.Split('.') | ForEach-Object { [int]$_ }
switch ($Bump) {
    'major' { $parts = @(($parts[0] + 1), 0, 0) }
    'minor' { $parts = @($parts[0], ($parts[1] + 1), 0) }
    'patch' { $parts = @($parts[0], $parts[1], ($parts[2] + 1)) }
}
if ($Bump -ne 'none') { $code++ }

$newPrefix = $parts -join '.'
$version = if ($suffix) { "$newPrefix-$suffix" } else { $newPrefix }
$tag = "v$version"

if (-not $DryRun) {
    gh release view $tag --repo $repo *> $null
    if ($LASTEXITCODE -eq 0) { throw "$tag is already released. Pick another -Bump." }
}

Write-Host "Releasing $version (version code $code)" -ForegroundColor Cyan

if (-not $DryRun -and $Bump -ne 'none') {
    $props = $props -replace '<VersionPrefix>[^<]+</VersionPrefix>', "<VersionPrefix>$newPrefix</VersionPrefix>"
    $props = $props -replace '<VersionCode>[^<]+</VersionCode>', "<VersionCode>$code</VersionCode>"
    $props = $props -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$newPrefix.0</AssemblyVersion>"
    $props = $props -replace '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$newPrefix.`$(VersionCode)</FileVersion>"
    Write-Text $propsPath $props

    Write-Text $changelogPath ([regex]::Replace($changelog, '(?m)^## Unreleased(?=\r?$)', "## $version (version code $code)", 1))

    Invoke-Git add $propsPath $changelogPath
    Invoke-Git commit --quiet -m "chore(release): version $version, code $code"
}

if (-not $DryRun) {
    Write-Host 'Pushing, which runs the whole test suite once' -ForegroundColor Cyan
    Invoke-Git push --quiet origin master
}

# Build

foreach ($runtime in $runtimes) {
    & $publish -Runtime $runtime -SkipTests -MirrorRepo '' -CertThumbprint $CertThumbprint -TimestampUrl $TimestampUrl
    if (-not $?) { throw "Building $runtime failed." }
}

# Notes

$assets = foreach ($arch in @('x64', 'arm64', 'x86')) {
    foreach ($kind in @('setup', 'portable')) {
        $file = Join-Path $dist "TtsUtilWin-$version-$arch-$kind.zip"
        "$((Get-FileHash -Algorithm SHA256 $file).Hash.ToLowerInvariant())  $(Split-Path -Leaf $file)"
    }
}
$checksums = "``````n$($assets -join "`n")`n``````"

$notesPath = Join-Path $dist "RELEASE_NOTES_$tag.md"
if ($NotesFile) {
    $notes = (Read-Text $NotesFile).Replace('{{checksums}}', $checksums)
    if ($notes -notlike "*$($assets[0].Split(' ')[0])*") { throw "$NotesFile needs a {{checksums}} line for the hashes to go in." }
}
else {
    $entries = $unreleased.Groups[1].Value.Trim() -replace "`r`n", "`n"
    $lead = if ($Headline) { "**$Headline**`n`n" } else { '' }
    $notes = @"
$lead## Download

| File | For |
| --- | --- |
| ``TtsUtilWin-$version-x64-setup.zip`` | Unpack and run the installer. Start menu entry, upgrades in place, can download voices for you |
| ``TtsUtilWin-$version-x64-portable.zip`` | Unpack anywhere and run ``TtsUtilWin.exe``. Settings and voices stay in the folder |
| ``TtsUtilWin-$version-arm64-*.zip`` | The same two, for an ARM64 machine such as a Snapdragon laptop |
| ``TtsUtilWin-$version-x86-*.zip`` | The same two, for 32 bit Windows |

Windows 10 version 1607 or newer. No voice model has to be downloaded: the Microsoft voices
Windows already has work with the program as they are.

The installer is not code signed, so SmartScreen warns the first time it runs, and is known to
block it outright on some machines. The README says what to do about that.

## What changed

$entries

## Checksums

$checksums

Check one with ``Get-FileHash TtsUtilWin-$version-x64-setup.zip``.

---

TTS Util Win is a Windows port of [TTS Util](https://github.com/drmfinlay/tts-util-app) by Dane
Finlay, used under the Apache License 2.0. Speech comes from
[sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx). This application was coded using AI
assistance.
"@
}
Write-Text $notesPath $notes
Write-Host "Wrote $notesPath" -ForegroundColor Green

if ($DryRun) {
    Write-Host 'Dry run: nothing was committed, pushed or published.' -ForegroundColor Yellow
    return
}

# Publish

foreach ($runtime in $runtimes) {
    & $publish -Runtime $runtime -Publish -SkipBuild -MirrorRepo '' -NotesFile $notesPath
    if (-not $?) { throw "Publishing $runtime failed. Re-run Publish.ps1 -Publish -SkipBuild for it." }
}

Invoke-Git pull --quiet --ff-only origin master
Write-Host "Released ${version}: https://github.com/$repo/releases/tag/$tag" -ForegroundColor Green
