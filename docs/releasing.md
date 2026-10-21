[Documentation index](index.md) | [Usage](usage.md) | [Performance](performance.md) | [Build](building.md) | [Release](releasing.md) | [Develop](developing.md) | [Write a feature](writing-a-feature.md)

---

# 📦 Release

Cutting a release means building the artifacts, hashing them, attaching them to a GitHub
release and publishing the manifest the update check reads. Version numbers come from
`Directory.Build.props` and nowhere else, so a release starts by bumping the two values there.

## One command

```powershell
.\scripts\Release.ps1
```

That is the whole release. It checks that `gh` is signed in and that master is clean and up to
date with origin, bumps the minor version and the version code, renames the `Unreleased`
changelog heading to the new version, commits that and pushes it (the push runs the whole test
suite once), builds ARM64, 32 bit and x64 without testing again, writes
`dist\RELEASE_NOTES_v<version>.md` from the changelog with the hashes filled in, publishes the
three, and pulls the manifest commit back. It stops before changing anything when the
changelog has no `Unreleased` entries or the tag is already released.

```powershell
# A fix release, with a bold line at the top of the notes.
.\scripts\Release.ps1 -Bump patch -Headline 'The line list keeps up again.'

# Notes written by hand. A {{checksums}} line in them becomes the hash block.
.\scripts\Release.ps1 -NotesFile notes\draft.md

# Build and write the notes at the current version, with nothing committed or published.
.\scripts\Release.ps1 -DryRun
```

`-Bump` takes `major`, `minor` (the default), `patch` or `none`. `-CertThumbprint` signs the
builds, as below. When a publishing step fails part way, the builds and notes are still in
`dist\`, and the sections below cover finishing by hand with `-SkipBuild`.

## Publishing a build

```powershell
# Build, zip, hash, and write dist\latest.json without publishing anything.
.\scripts\Publish.ps1

# Copy the two hashes it printed into the notes, then publish what was just
# built. The update notice appears once the manifest lands.
.\scripts\Publish.ps1 -Publish -SkipBuild -NotesFile dist\RELEASE_NOTES_v0.3.0-beta.md
```

`-SkipBuild` publishes the zips already in `dist\` instead of making new ones. The setup program
carries a build time, so a rebuild changes both hashes, and the notes written from the previous
run would no longer match what is uploaded. Leave it off only on a run that is not publishing.

The manifest is pushed **after** the assets, so nobody is pointed at a download that is not
there yet. `distribution\README.md` is the readme laid down in the program folder and packed
into the portable zip, and `distribution\CHANGELOG.md` is the changelog compiled into the
program.

## The other two architectures

`-Runtime win-arm64` and `-Runtime win-x86` package those builds and attach them to the release
under their own filenames. Build all three before publishing any of them, so the manifest each
publishing run writes names every architecture rather than only the ones packaged so far.

```powershell
.\scripts\Publish.ps1 -Runtime win-arm64
.\scripts\Publish.ps1 -Runtime win-x86
.\scripts\Publish.ps1
.\scripts\Publish.ps1 -Publish -SkipBuild -NotesFile dist\notes.md -Runtime win-arm64
.\scripts\Publish.ps1 -Publish -SkipBuild -NotesFile dist\notes.md -Runtime win-x86
.\scripts\Publish.ps1 -Publish -SkipBuild -NotesFile dist\notes.md
```

The manifest carries a download per architecture under `architectures`, keyed `x64`, `arm64`
and `x86`, and a copy takes the one matching the process it is running in. A release with no
build for that architecture is a link to the release page rather than an offer to install.

Each run attaches three files: the setup zip, the portable zip and the setup program itself.
The zips are what the manifest names and what the in app update downloads. The bare
`TtsUtilWin-<version>-<arch>-setup.exe` is there for winget and anything else that wants an
installer URL it can hand straight to Windows.

`setup` and `portable` stay at the top level, naming the x64 pair. That is where a copy built
before 0.13.0 looks, and it reads nothing else, so those two fields cannot be moved or renamed
while such copies are still checking.

Any run holding the x64 zips in `dist\` writes the manifest, and a run without them uploads its
assets and leaves the manifest alone. The x64 zips are what the older copies are offered.

## Where a build looks for its update

Every build reads a `latest.json` URL that was compiled into it, so a copy already installed
keeps asking the address it shipped with, whatever the current one is. 0.12.0-beta moved that
address from `tts-util-win-releases` to this repository, which makes it an overlap rather than a
switch: a release goes to both, with the same tag, the same assets, the same hashes, and a
manifest in each naming its own copies.

`Publish.ps1` does both on its own. `-Repo` is this repository and `-MirrorRepo` is the retired
one, and passing an empty `-MirrorRepo` skips the mirror, which is what a release after the
archiving does. An archived repository accepts no pushes, so the last manifest written there is
the one it keeps: a copy built before 0.12.0-beta is offered 0.12.0-beta, takes it, and asks
this repository from then on.

## The changelog

`distribution\CHANGELOG.md` is an embedded resource, so the Updates tab lists it without
fetching anything, and every `## ` heading in it becomes a release row there. Changes land under
the `Unreleased` heading at the top as they are made, and the release step renames that heading
to the version and the date. A release whose changelog entry is still called `Unreleased` ships
a program that says so.

## Code signing

The build is unsigned by default, which is why SmartScreen warns the first time a setup runs,
and why it sometimes blocks the setup outright with no way through the dialog. The public README
tells readers how to turn **Check apps and files** off in Windows Security when that happens.
Both scripts take a certificate thumbprint and sign with it when one is given:

```powershell
.\scripts\Publish.ps1 -Publish -NotesFile dist\notes.md -CertThumbprint ABC123DEF456
```

The certificate is looked up in the current user's store by thumbprint, so nothing secret is
written down here. `TtsUtilWin.exe` is signed before the installer is compiled, so the
installed copy carries the signature too, and the setup is signed after Inno Setup builds it.
Both signatures are timestamped, which keeps them valid after the certificate expires, and
both are verified with `signtool verify /pa` before the build is allowed to finish. Leave
`-CertThumbprint` off and the build is unsigned exactly as before.
