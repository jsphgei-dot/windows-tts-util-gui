[Documentation index](index.md) | [Usage](usage.md) | [Performance](performance.md) | [Build](building.md) | [Release](releasing.md) | [Develop](developing.md) | [Write a feature](writing-a-feature.md)

---

# Documentation

Everything written down about TTS Util Win, in one place. The
[repository README](../README.md) is the short version: what the program is, how to install it
and where to go next.

| Document | For | What is in it |
| --- | --- | --- |
| [Usage](usage.md) | Anybody using the program | The tab by tab manual |
| [Speed and machine requirements](performance.md) | Anybody wondering what it costs to run | Measured render speeds, the thread rule, minimum and recommended specs |
| [Build from source](building.md) | Anybody compiling it | The SDK, the scripts, the switches, the output |
| [Release](releasing.md) | Whoever cuts a release | Publishing, the update manifest, code signing |
| [Develop and test](developing.md) | Anybody changing the code | Projects, tests, git hooks, versioning |
| [Writing a feature](writing-a-feature.md) | Anybody adding one | Every step from the issue to the last test, followed through one real feature |
| [Contributing](../CONTRIBUTING.md) | Anybody sending a change | Where a change belongs, the style, the commit format |
| [Changelog](../distribution/CHANGELOG.md) | Anybody wondering what changed | Every release, newest first |

## Usage, section by section

* [Voices](usage.md#voices), the Microsoft voices Windows brings, the curated neural models,
  other languages, and multi speaker models
* [Writing and editing](usage.md#writing-and-editing), the tabs and the editing toolbar
* [Reading text](usage.md#reading-text)
* [The media keys](usage.md#the-media-keys)
* [Moving about while it reads](usage.md#moving-about-while-it-reads)
* [Files and PDFs](usage.md#files-and-pdfs), including OCR of scanned pages
* [Saving audio](usage.md#saving-audio)
* [Scripts](usage.md#scripts), the saved library and batch conversion
* [The queue](usage.md#the-queue)
* [Aliases](usage.md#aliases)
* [Settings](usage.md#settings)
* [The status bar](usage.md#the-status-bar)
* [Updates](usage.md#updates)

## Elsewhere in the repository

* `voices/README.md` explains what makes a folder a voice, for anybody adding a model by hand.
* `distribution/README.md` is the readme that ships inside the program folder and the portable
  zip, written for people running the program rather than building it, and
  `distribution/CHANGELOG.md` is the changelog, which is also compiled into the program and read
  by the Updates tab. `distribution/retired-repo-README.md` is the landing page of the retired
  distribution repository, which is published nowhere else.
* `LICENSE`, `NOTICE` and `THIRD-PARTY-NOTICES.txt` travel with every build.
