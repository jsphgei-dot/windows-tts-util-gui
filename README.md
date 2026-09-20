# TTS Util Win

![version](https://img.shields.io/badge/version-0.12.0--beta-blue)
![platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011%20%C2%B7%20x64%20%7C%20ARM64%20%7C%20x86-0078D6)
![built with](https://img.shields.io/badge/.NET-10.0-512BD4)
![tests](https://img.shields.io/badge/tests-531%20passing-brightgreen)
![license](https://img.shields.io/badge/license-Apache%202.0-lightgrey)

> Read anything you can type, paste or open out loud, entirely on your own machine.

## ✨ Highlights

* **Offline, always.** Your machine is responsible for running the entire generation, nothing is uploaded or downloaded to generate audio. It reaches the network for two purposes only: to
  download a voice you asked for, and for the optional update check, which sends out nothing and
  can be switched off.
* **Real neural voices.** Piper, Kokoro and Matcha models run through the
  [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) engine locally, including one with over 900 speakers.
* **Large library of voice models.** The voice you install in the app decides the speed, and the performance range is
  wide. On my computer, the heaviest neural voice, [Kokoro](https://github.com/hexgrad/kokoro), renders 1.4x to 4.3x
  faster than real time depending on how many threads were given in the settings, and a lighter [Piper](https://github.com/rhasspy/piper) voice manages
  13.6x to 35.5x realtime speeds. The three voices Windows carries out of the box generate between 372x and 592x real time.
  Playback needs only 1.0x (realtime), so a lighter voice is what will let weaker machines be able to still do heavy workloads, and it's what turns writing an MP3 of a long script into seconds rather than minutes. The
  [full measurements](docs/performance.md) cover every voice and thread count.
* **Reads however you want.** Symbols, links and stray punctuation will be filtered out as the default reading setting
  instead of being spoken as "dollar", "hash" and "https colon slash slash".
* **Text, files, PDFs and scans.** Scanned pages go through the OCR already in Windows.
* **Works anywhere, anytime.** Play it back on your PC, or write it to an MP3 and listen on your phone. Perfect for studying on the go.
* **Queue and repeat.** Line up a run of saved texts (called scripts), loop one of them, or loop the whole list.
* **Batch conversion.** Select several saved scripts and batch write them all to audio files so you can generate consecutive files unattended.
* **It keeps your place.** Every text tab is still there when you open the program again.
* **Batch audio generation while remaining usable.** New tab gives you another text window, and each one writes its audio
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

**No voice model required.** Windows already carries three voices,
David, Zira and Mark. The installer's components page shows them at the top as ticked, greyed
out rows, meaning they are on the machine with no download needed. Neural voices are optional but recommended for a better experience.

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
The same version offers a repair. An older version warns that it would downgrade and asks for
confirmation. Only one architecture can be installed at a time, so a setup that finds another one already there offers to remove it first.

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
