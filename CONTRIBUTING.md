# Contributing

Thanks for looking. This is a small project with one author, so the bar is not high ceremony,
it is just that a change should arrive in a shape somebody else can read a year from now.

Everything here is enforced by git hooks rather than by review, so you find out in a second
rather than in a comment thread.

## Before you write any code

Open an issue first for anything larger than a fix. A feature that takes a day to write and
does not fit the program is worse for you than for me, and the awkward part of this project is
almost never the code: it is deciding what a setting should do when the text it is given is
strange. Small fixes, typos, a wrong tooltip, a test that asserts nothing, go straight to a
pull request with no preamble.

Bugs and voice suggestions go to
[issues](https://github.com/jsphgei-dot/tts-util-win/issues). A report that names the voice, the
setting and the text that misbehaved is worth a great deal.

[docs/writing-a-feature.md](docs/writing-a-feature.md) walks the whole path, from the issue to
the last test, through one real feature. Read that if you want the order. This page is the
rules.

## Getting set up

You need the .NET 10 SDK and Windows 10 version 1607 or newer. Nothing else.
[docs/building.md](docs/building.md) has the full build section, including the installer and the
voice download script, and [docs/index.md](docs/index.md) lists everything else that is written
down.

```powershell
git clone https://github.com/jsphgei-dot/tts-util-win.git
cd tts-util-win
.\scripts\InstallHooks.ps1     # run this once, before your first commit
dotnet build TtsUtilWin.sln
```

`InstallHooks.ps1` points `core.hooksPath` at `.githooks`, so the hooks travel with the
repository. Skipping it means your first push fails checks you could have seen locally.

No voice model is needed to build or to run the tests. The three Microsoft voices that come
with Windows are enough to hear the program work.

## The shape of the codebase

| Project | Target | What belongs in it |
| --- | --- | --- |
| `TtsUtil.Core` | `net10.0` | Text chunking, filters, silence rules, aliases, wave IO, voice discovery, the sherpa engine, settings, the update manifest. No UI types and no Windows APIs |
| `TtsUtil.App` | `net10.0-windows10.0.19041.0` | The WPF window, NAudio playback, the Windows speech engine, media transport controls, Windows OCR, file dialogs |
| `TtsUtil.Core.Tests` | `net10.0` | Tests for the platform neutral half |
| `TtsUtil.App.Tests` | `net10.0-windows10.0.19041.0` | Tests that drive the real window on an STA dispatcher thread |

The one rule worth stating: **logic goes in Core, and Core stays free of Windows.** It is the
half that could run somewhere other than Windows one day, and every Windows API that leaks into
it costs that option. Where Core genuinely needs a platform service it takes an interface and
`TtsUtil.App` supplies it, which is what `IPageImageOcr` is for.

`TtsUtil.App` is large and split across partial classes of `MainWindow`, one file per area
(`MainWindow.Documents.cs`, `MainWindow.Updates.cs`, and so on). A new area of behavior is a new
partial file rather than more lines in `MainWindow.xaml.cs`.

## Style

`.editorconfig` carries the formatting and `dotnet format` enforces it, so there is nothing to
argue about there. What the tooling cannot check:

* **Name things for what they do to the user, not for the pattern.** `SizeTabs`, not
  `TabLayoutManager`. The codebase has no `Helper`, `Manager` or `Util` classes and would like
  to keep it that way.
* **Comments say what, not why, and stop at two lines.** A doc comment on a method that is not
  obvious is welcome. A paragraph explaining the reasoning is not: that belongs in the commit
  message or in the decision log.
* **Every source file carries the license header** that the existing files carry. Copy it.
* Files are UTF-8 with CRLF endings. The hook rejects a file written with LF.

## Tests

```powershell
dotnet test tests\TtsUtil.Core.Tests
dotnet test tests\TtsUtil.App.Tests -p:SkipTests=true
```

The suite runs in seconds, needs no display and needs no voice model. The UI tests build a real
`MainWindow` on an STA dispatcher thread and raise Click events on actual controls, so a test
can press a button rather than call the handler behind it. Prefer that.

**The standing rule for a new test: name the change that would make it fail.** If the answer is
nothing a person would plausibly write, the test is not worth its maintenance. Tests that assert
a constructor assigned its argument, that an empty list is empty, or that a string format string
formatted, get removed when they are found. A test that pins down real behavior, especially
behavior that was once wrong, is worth several of those.

Where a feature has one pure part and one layout part, factor the pure part out and test that.
`MainWindow.TabWidth(room, count)` exists in that shape for exactly this reason.

`SherpaTtsEngine` has no automated test, since exercising it needs a multi hundred megabyte
model and would make every build slow and machine dependent. Changes there are verified by hand,
and saying so in the pull request is fine.

## Commits

Conventional Commits, checked by the `commit-msg` hook:

```
type(scope): summary in lowercase, imperative, no trailing period
```

* Types: `feat`, `fix`, `docs`, `test`, `refactor`, `perf`, `build`, `ci`, `chore`, `style`,
  `revert`. The scope is optional and lowercase.
* The subject is 72 characters or fewer and does not start with a capital.
* The body, if there is one, is **200 characters or fewer**. Detail belongs in the code, in the
  tests or in the documentation, not in a commit message nobody will scroll back to.
* No tool attribution lines. Commits are authored by the person who sent them.

One commit per idea. A refactor and the feature it enables are two commits, which makes the
feature readable.

## What the hooks will tell you

| Hook | Checks |
| --- | --- |
| `pre-commit` | Trailing whitespace and conflict markers, no file over 5 MB, no build output from `dist`, `artifacts` or `voices`, the installer version tests when an `.iss` file is staged, `dotnet format`, a build with warnings as errors, and the Core tests |
| `commit-msg` | The subject, the body length, and the attribution rule above |
| `pre-push` | The full test suite, Core and App |

Formatting, build and tests are skipped when a commit stages no code, so a documentation change
is quick. Bypass one commit with `--no-verify` if you must, and expect to be asked why.

## Things worth knowing before you touch them

* **The voice catalog lives in three places** on purpose: `DownloadableVoices` in the program,
  `installer\TtsUtilWin.iss` for the setup checkboxes, and `scripts\FetchVoices.ps1`. Three
  languages, three run times, no shared file that could be read by all three. Adding a voice
  means three edits, and a Core test checks the C# list is internally consistent but cannot
  check the three against each other.
* **The changelog is compiled into the program.** `distribution\CHANGELOG.md` is an embedded
  resource and the Updates tab lists it. Every `## ` heading becomes a release in that list, so
  the heading format matters. Put user visible changes under the `Unreleased` heading at the
  top and the release step renames it.
* **The two READMEs are different documents.** `README.md` at the root is the front door of
  the repository and stays short, with the detail in `docs\`. `distribution\README.md` is for
  somebody downloading a build: it is copied into the portable folder and installed beside the
  program, so it must stay free of anything about the source.
  `distribution\retired-repo-README.md` is neither, it is the landing page of the archived
  download repository, and nothing ships it.
* **Settings need their help text.** Every setting is explained behind a question mark, and the
  text lives in `SettingsHelp` in Core rather than in XAML so it can be tested. A new setting
  without help text is not finished.
* **Scripts are plain text files** and the editing toolbar changes words rather than appearance.
  Anything that cannot survive being saved as `.txt` and cannot be spoken does not belong there.

## Pull requests

Branch from `master`, keep the branch to one subject, and say in the description what a reviewer
should try by hand, if anything. Mention explicitly when a change was verified against a real
voice model, because the test suite cannot do that for you.

Release cutting, version bumps and the update manifest are handled by the maintainer. Please
leave `Directory.Build.props` alone in a pull request.

## Licensing

This project is Apache License 2.0, matching the original TTS Util by Dane Finlay. By sending a
pull request you agree that your contribution is licensed the same way. There is no separate
contributor agreement to sign.

If your change adds a dependency, it has to be compatible with Apache 2.0 and it has to be named
in `THIRD-PARTY-NOTICES.txt`, which ships beside every build. A GPL dependency cannot be linked
in, whatever else it has going for it. Please raise a dependency in an issue before writing
against it: most of them turn out not to be worth the notice file entry and the extra megabytes
in a self contained executable.

Voice models carry their own licenses and are not covered by any of the above. Only public
domain, CC BY and Apache licensed models go in the download catalog.
