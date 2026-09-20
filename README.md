# TTS Util Win

![version](https://img.shields.io/badge/version-0.12.0--beta-blue)
![platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011%20%C2%B7%20x64%20%7C%20ARM64%20%7C%20x86-0078D6)
![built with](https://img.shields.io/badge/.NET-10.0-512BD4)
![tests](https://img.shields.io/badge/tests-478%20passing-brightgreen)
![license](https://img.shields.io/badge/license-Apache%202.0-lightgrey)

> Read anything you can type, paste or open out loud, entirely on your own machine.

## ✨ Highlights

* **Offline, always.** The voice runs locally. Nothing is uploaded, nothing is fetched while
  you read, and no system speech engine is involved. It reaches the network twice only: to
  download a voice you asked for, and for the update check on the way in, which sends nothing and
  can be switched off.
* **Real neural voices.** Piper, Kokoro and Matcha models through
  [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx), including one with over 900 speakers.
* **Fast, and how fast is up to you.** The voice you pick decides the cost, and the range is
  wide. On the desktop the figures were taken on, the heaviest neural voice renders 1.4x to 4.3x
  faster than real time depending on the threads it is given, a lighter Piper voice manages
  13.6x to 35.5x, and the three voices Windows already carries run between 372x and 592x.
  Playback needs only 1.0x, so a lighter voice is what lets a modest machine stay ahead of the
  listener, and what turns writing an MP3 of a long script into seconds rather than a wait. The
  [full measurements](docs/performance.md) cover every voice and thread count.
* **Reads what a reader would read.** Symbols, links and stray punctuation are filtered out
  instead of being spoken as "dollar", "hash" and "https colon slash slash".
* **Text, files, PDFs and scans.** Scanned pages go through the OCR already in Windows.
* **Listen or keep it.** Play it back, or write the same input to an MP3 or a wave file.
* **Queue and repeat.** Line up a run of scripts, loop one of them, or loop the lot.
* **Batch conversion.** Tick several saved scripts and write them all to audio files at once.
* **It keeps your place.** Every text tab is still there when you open the program again, audio
  written from a script is offered that script's title as its file name, and the words behind a
  recording are saved as a script under the name you gave the file.
* **Each script remembers its voice.** Saving a script keeps the voice, the speaker and the
  talking speed with it, and opening or converting it reads it back the way it sounded.
* **Several texts at once.** New tab gives you another text window, and each one writes its audio
  in the background on a voice of its own, so you can set several recordings going and carry on
  reading in another tab.
* **Installer or portable.** One build gives both, and the portable copy touches nothing
  outside its own folder.

## 📖 Overview

TTS Util Win turns text into speech on a Windows machine with no network access and no Windows
voices. You give it typed text, the clipboard, a text file or a PDF; it picks the text apart
into sentences, filters out what should not be spoken, and streams the audio from a neural
model as it is generated, so a long document starts playing in about a second rather than
after the whole thing has been synthesized.

The speech comes from [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) running ONNX models
on the CPU. Voices are ordinary folders you can add, remove and back up. The program ships
with none and offers a curated list to download, so you choose what lands on the disk.

### How it relates to TTS Util

This is a Windows port of [TTS Util](https://github.com/drmfinlay/tts-util-app), the Android
application by Dane Finlay, and it keeps that program's ideas: read aloud or save to a file,
custom silence around sentences and line endings, filters for the things a reader would skip.
What is new here is the engine (Android hands text to the system TTS service, this carries its
own), PDF and OCR import, the script library and the queue.

### Who made it

Written by [jsphgei-dot](https://github.com/jsphgei-dot). The original TTS Util is by Dane
Finlay. See `NOTICE` for the full attribution.

This application was coded using AI assistance.

## ⬇️ Install

**Download a build.** Builds are published to
[Releases](https://github.com/jsphgei-dot/tts-util-win/releases/latest), as
`TtsUtilWin-<version>-<arch>-setup.zip` and `TtsUtilWin-<version>-<arch>-portable.zip`, each
with its SHA256 on the release page, where `<arch>` is `x64`, `arm64` or `x86`. The installer
is also attached on its own, as `TtsUtilWin-<version>-<arch>-setup.exe`, for package managers
and anything else wanting a URL it can hand straight to Windows. Nothing is committed to this
repository: `dist/` is ignored, so the executable and the installer are always either
downloaded from a release or built locally.

**Installer.** `TtsUtilWin-<version>-<arch>-setup.exe`, inside the setup zip, asks on its
first page whether to install for all users (needs administrator) or just for you
(recommended, no prompt), then adds a Start menu entry, an optional desktop shortcut, and an
entry in Settings, Apps. Settings go to `%APPDATA%\TtsUtilWin`.

**No voice model has to be downloaded.** Windows already carries three Microsoft voices,
Microsoft David, Microsoft Zira and Microsoft Mark, and the program speaks with them as they
are. The installer's components page shows them at the top as ticked, greyed out rows, which
say they are on the machine already and cost nothing to install. Everything under them is
optional.

The rest of that page lists the curated neural voices with a checkbox and a size each.
"Program and the three recommended neural voices" ticks the permissively licensed defaults,
"Program only, nothing downloaded" ticks none, and "Choose voices" leaves every box to you.
Setup downloads what is ticked and extracts it into `voices` beside the program, skipping any
model already present. A failed download warns and lets the installation finish. Uninstalling
removes the voices it installed.

**Portable.** Copy the `TtsUtilWin` folder anywhere. The `portable.txt` file beside the
executable keeps settings and voices inside that folder, so nothing touches your profile.

```
TtsUtilWin\
  TtsUtilWin.exe        the app, the .NET runtime, and the sherpa native libraries
  settings.json         written on first run, beside the exe when that is writable
  voices\
    vits-piper-en_US-ljspeech-high\
    kokoro-en-v0_19\
  scripts\
    FetchVoices.ps1     the downloader, which fills the voices folder above
```

Saved scripts land in that same `scripts` folder, as plain `.txt` files.

**Upgrading.** Setup recognizes an existing installation by its application id. A newer setup
upgrades in place, keeping the folder, shortcuts, settings and the voices already downloaded.
The same version offers a repair. An older setup warns that it would downgrade and asks for
confirmation. A running copy is closed through the Restart Manager rather than failing on a
locked file. There is no need to uninstall first. Only one architecture can be installed at a
time, so a setup that finds another one already there offers to remove it first.

**Requirements.** Windows 10 or 11, on x64, ARM64 or x86. The published executable is self
contained and needs no runtime installed. OCR of scanned PDFs uses the Windows OCR engine,
which needs a language pack, the kind Windows installs with its display languages.

## 🎧 Usage

Type or paste something, pick a voice, press **Read**. Everything past that, the tabs, the
queue, the script library, aliases, PDFs and OCR, saving to audio, and every setting, is in the
[usage guide](docs/usage.md).

## 🛠️ Build from source

Building takes about a minute and needs only the .NET 10 SDK.

```powershell
.\scripts\FetchVoices.ps1 -Default     # about 490 MB, optional
.\scripts\BuildPortable.ps1            # portable folder in dist\TtsUtilWin-x64
.\dist\TtsUtilWin-x64\TtsUtilWin.exe
```

Add `-Runtime win-arm64` or `-Runtime win-x86` for the other two processors. The installer, the
build switches, the output sizes and the signing options are in
[docs/building.md](docs/building.md).

## 📚 Documentation

| Document | What is in it |
| --- | --- |
| [Documentation index](docs/index.md) | Everything here, with the usage guide broken out section by section |
| [Usage](docs/usage.md) | The tab by tab manual |
| [Speed and machine requirements](docs/performance.md) | Measured render speeds, the thread rule, minimum and recommended specs |
| [Build from source](docs/building.md) | The SDK, the scripts, the switches, the output |
| [Release](docs/releasing.md) | Publishing, the update manifest, code signing |
| [Develop and test](docs/developing.md) | Projects, tests, git hooks, versioning |
| [Contributing](CONTRIBUTING.md) | Where a change belongs, the style, the commit format |
| [Changelog](distribution/CHANGELOG.md) | Every release, newest first |

## 🤝 Contributing and feedback

Bug reports and voice suggestions are welcome through
[issues](https://github.com/jsphgei-dot/tts-util-win/issues). A report that names the voice, the
setting and the text that misbehaved is worth a great deal, since most of the awkward cases live
in the text rather than in the code.

Code is welcome too. [CONTRIBUTING.md](CONTRIBUTING.md) covers the setup, where a change
belongs, the commit format and what the git hooks check before they let a commit through. Run
`.\scripts\InstallHooks.ps1` before your first commit so those gates run locally rather than
surprising you later.

## 📖 Further reading

* [TTS Util](https://github.com/drmfinlay/tts-util-app), the Android original.
* [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx), the engine, and its
  [voice catalog](https://github.com/k2-fsa/sherpa-onnx/releases/tag/tts-models).

## ⚖️ License

Apache 2.0, the same license as the original TTS Util by Dane Finlay and as sherpa-onnx.

Three files travel with every build, in the installed folder and in the portable folder alike:
`LICENSE` is the Apache 2.0 text, `NOTICE` names the work this one is derived from, and
`THIRD-PARTY-NOTICES.txt` carries the license of every component that ships inside the
executable (sherpa-onnx, ONNX Runtime, PdfPig, NAudio, and the .NET runtime itself).

Voice models are not covered by any of that. Each carries its own license, listed in the
[usage guide](docs/usage.md#voices) and restated in the `LICENSE` or `MODEL_CARD` file inside
the model folder.
