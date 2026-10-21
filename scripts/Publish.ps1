<#
.SYNOPSIS
    Packages a release and writes the manifest the program checks for updates.

.DESCRIPTION
    Builds the portable folder and the setup program, zips both, hashes them, and writes
    latest.json. With -Publish it also creates the GitHub release and pushes the manifest, which
    is what makes the update notice appear.

    The release goes to two repositories while copies built before 0.12.0 are still worth
    serving. Those ask the retired distribution repository for their manifest, so it gets the
    same tag, the same assets, and a manifest of its own naming its own copies. Pass an empty
    -MirrorRepo once that repository is archived, and the mirror step is skipped.

.EXAMPLE
    .\Publish.ps1

.EXAMPLE
    .\Publish.ps1 -Publish -SkipBuild -NotesFile dist\RELEASE_NOTES_v0.3.0-beta.md

.EXAMPLE
    .\Publish.ps1 -Publish -NotesFile dist\notes.md -CertThumbprint ABC123DEF456
#>
[CmdletBinding()]
param(
    [string]$Repo = 'jsphgei-dot/tts-util-win',

    [string]$MirrorRepo = 'jsphgei-dot/tts-util-win-releases',

    [ValidateSet('win-x64', 'win-arm64', 'win-x86')]
    [string]$Runtime = 'win-x64',

    [string]$NotesFile,

    [switch]$Publish,

    [switch]$SkipTests,

    [switch]$SkipBuild,

    [switch]$SkipManifest,

    [string]$CertThumbprint,

    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root 'dist'
$arch = $Runtime -replace '^win-', ''

# The version lives in one place, so read it rather than asking for it again here.
$props = Get-Content (Join-Path $root 'Directory.Build.props') -Raw
$prefix = [regex]::Match($props, '<VersionPrefix>([^<]+)</VersionPrefix>').Groups[1].Value
$suffix = [regex]::Match($props, '<VersionSuffix>([^<]+)</VersionSuffix>').Groups[1].Value
$code = [int][regex]::Match($props, '<VersionCode>([^<]+)</VersionCode>').Groups[1].Value
$version = if ($suffix) { "$prefix-$suffix" } else { $prefix }
$tag = "v$version"

# Checked before the build rather than after it, so a missing file costs a second, not a build.
if ($Publish) {
    if (-not $NotesFile) { throw 'Publishing needs -NotesFile pointing at the release notes.' }
    if (-not (Test-Path $NotesFile)) { throw "No notes at $NotesFile." }
}

Write-Host "Packaging $version $arch (version code $code)" -ForegroundColor Cyan
if (-not $CertThumbprint) {
    Write-Host 'No certificate given, so this release is unsigned and SmartScreen will warn.' -ForegroundColor Yellow
}

$portableZip = Join-Path $dist "TtsUtilWin-$version-$arch-portable.zip"
$setupExe = Join-Path $dist "TtsUtilWin-$version-$arch-setup.exe"
$setupZip = Join-Path $dist "TtsUtilWin-$version-$arch-setup.zip"

# The exe and the setup are both signed inside the build, before either is zipped, so the
# hashes written below belong to the signed files.
if ($SkipBuild) {
    # The setup program carries a build time, so a rebuild changes both hashes and the notes
    # written from the previous run stop matching. This publishes what is already in dist.
    foreach ($asset in @($portableZip, $setupZip, $setupExe)) {
        if (-not (Test-Path $asset)) { throw "-SkipBuild needs $asset, which is not there." }
    }
}
else {
    & (Join-Path $PSScriptRoot 'BuildPortable.ps1') -Runtime $Runtime -Installer -SkipTests:$SkipTests `
        -CertThumbprint $CertThumbprint -TimestampUrl $TimestampUrl
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

    if (-not (Test-Path $setupExe)) { throw "No setup program at $setupExe." }

    Remove-Item $portableZip, $setupZip -ErrorAction SilentlyContinue
    Compress-Archive -Path (Join-Path $dist "TtsUtilWin-$arch") -DestinationPath $portableZip
    Compress-Archive -Path $setupExe -DestinationPath $setupZip
}

function Get-Sha256([string]$path) { (Get-FileHash -Algorithm SHA256 -Path $path).Hash.ToLowerInvariant() }

$architectures = @('x64', 'arm64', 'x86')

# The pair for one architecture, or nothing when that build is not in dist.
function Get-ArchZips([string]$a)
{
    $s = Join-Path $dist "TtsUtilWin-$version-$a-setup.zip"
    $p = Join-Path $dist "TtsUtilWin-$version-$a-portable.zip"
    if ((Test-Path $s) -and (Test-Path $p)) { return [pscustomobject]@{ Setup = $s; Portable = $p } }
    return $null
}

function New-Download([string]$repo, [string]$path)
{
    return [ordered]@{
        url    = "https://github.com/$repo/releases/download/$tag/$(Split-Path -Leaf $path)"
        bytes  = (Get-Item $path).Length
        sha256 = Get-Sha256 $path
    }
}

# The program reads this file, and refuses any download whose hash does not match what is here.
# A repository gets a manifest naming its own copies of the assets, never the other one's.
function New-Manifest([string]$repo)
{
    $built = [ordered]@{}
    foreach ($a in $architectures) {
        $zips = Get-ArchZips $a
        if ($zips) {
            $built[$a] = [ordered]@{
                setup    = New-Download $repo $zips.Setup
                portable = New-Download $repo $zips.Portable
            }
        }
    }

    if (-not $built.Contains('x64')) { throw 'The manifest needs the x64 zips, which are not in dist.' }

    # setup and portable are the x64 pair, which is where a copy built before 0.13.0 looks.
    return [ordered]@{
        versionName   = $version
        versionCode   = $code
        releaseUrl    = "https://github.com/$repo/releases/tag/$tag"
        setup         = $built['x64'].setup
        portable      = $built['x64'].portable
        architectures = $built
    }
}

$thisSetupHash = Get-Sha256 $setupZip
$thisPortableHash = Get-Sha256 $portableZip

Write-Host "Packaged $arch" -ForegroundColor Green
Write-Host "  setup    $thisSetupHash"
Write-Host "  portable $thisPortableHash"

# Every architecture in dist goes into the manifest, so the x64 run is not the only one that
# can write it. A run without the x64 zips leaves the file alone.
$manifestPath = Join-Path $dist 'latest.json'
if (Get-ArchZips 'x64') {
    $manifest = New-Manifest $Repo
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -Path $manifestPath -Encoding utf8
    Write-Host "Wrote $manifestPath naming $($manifest.architectures.Keys -join ', ')" -ForegroundColor Green
}
else {
    Write-Host 'No x64 zips in dist, so no manifest was written.' -ForegroundColor Yellow
}

if (-not $Publish) {
    Write-Host 'Nothing published. Re-run with -Publish when the notes are ready.' -ForegroundColor Yellow
    return
}

# A build the notes were not written from is caught here rather than on the release page.
$notes = Get-Content $NotesFile -Raw
foreach ($hash in @($thisSetupHash, $thisPortableHash)) {
    if ($notes -notlike "*$hash*") {
        throw "The notes do not carry $hash. Copy the hashes above into $NotesFile, then re-run with -SkipBuild."
    }
}

# The bare setup program goes up beside the zips. Package managers fetch an installer
# directly, and the in app update reads the zip named in the manifest.
function Send-Assets([string]$repo)
{
    $assets = @($setupZip, $portableZip, $setupExe)

    Write-Host "Creating $tag in $repo" -ForegroundColor Cyan
    # A second architecture lands on the release the first one made rather than failing on it.
    gh release view $tag --repo $repo *> $null
    if ($LASTEXITCODE -eq 0) {
        gh release upload $tag @assets --repo $repo --clobber
        if ($LASTEXITCODE -ne 0) { throw "Uploading to the existing release in $repo failed." }
    }
    else {
        gh release create $tag @assets --repo $repo --title "TTS Util Win $version" `
            --notes-file $NotesFile --prerelease
        if ($LASTEXITCODE -ne 0) { throw "Creating the release in $repo failed." }
    }
}

# The manifest lands after the assets, or a reader could be sent to a download that is not
# there yet.
function Send-Manifest([string]$repo, $body)
{
    $clone = Join-Path $env:TEMP "ttsutilwin-manifest-$([guid]::NewGuid().ToString('N'))"
    gh repo clone $repo $clone -- --depth 1
    if ($LASTEXITCODE -ne 0) { throw "Cloning $repo failed." }

    # A machine with no global identity still has one here, and the clone is told about it.
    $name = git -C $root config user.name
    $mail = git -C $root config user.email
    if ($name) { git -C $clone config user.name $name }
    if ($mail) { git -C $clone config user.email $mail }

    $body | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $clone 'latest.json') -Encoding utf8
    git -C $clone add latest.json
    git -C $clone diff --cached --quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "The manifest in $repo already says this." -ForegroundColor Yellow
        Remove-Item $clone -Recurse -Force
        return
    }
    git -C $clone commit -m "chore(release): point the manifest at $version"
    if ($LASTEXITCODE -ne 0) { throw "Committing the manifest in $repo failed." }
    git -C $clone push
    if ($LASTEXITCODE -ne 0) { throw "Pushing the manifest to $repo failed, so the release is not offered yet." }
    Remove-Item $clone -Recurse -Force
}

Send-Assets $Repo
if ($MirrorRepo) { Send-Assets $MirrorRepo }

# The manifest names the architectures whose zips are in dist, so the run that publishes last
# is the one that offers them all. Without the x64 zips there is nothing to write.
if ($SkipManifest) {
    Write-Host 'Assets uploaded. The manifest was left alone, as asked.' -ForegroundColor Yellow
    return
}

if (-not (Get-ArchZips 'x64')) {
    Write-Host 'Assets uploaded. The manifest was left alone, which needs an x64 build.' -ForegroundColor Yellow
    return
}

Send-Manifest $Repo (New-Manifest $Repo)
if ($MirrorRepo) { Send-Manifest $MirrorRepo (New-Manifest $MirrorRepo) }

Write-Host "Published $version." -ForegroundColor Green
