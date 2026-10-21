[Documentation index](index.md) | [Usage](usage.md) | [Performance](performance.md) | [Build](building.md) | [Release](releasing.md) | [Develop](developing.md) | [Write a feature](writing-a-feature.md)

---

# 🧵 Writing a feature, start to finish

Every step between deciding a feature is worth having and pushing the last test, in the order
they happen, with one real feature followed the whole way through.

The worked example is commit
[`1bf0f60`](https://github.com/jsphgei-dot/tts-util-win/commit/1bf0f60), **the Voice column in
the script list**. It is a good specimen: small enough to read in one sitting, and it still
touches the model, the window, the markup, the tests and the documentation, which is most of
what any feature touches. Five files, 46 lines added.

```
README.md                                  |  7 ++++++-
src/TtsUtil.App/MainWindow.xaml            |  9 +++++++++
src/TtsUtil.App/MainWindow.xaml.cs         |  3 ++-
src/TtsUtil.App/ScriptRow.cs               | 12 +++++++++++-
tests/TtsUtil.App.Tests/ScriptsTabTests.cs | 18 ++++++++++++++++++
```

[Develop and test](developing.md) is the reference for the commands and the hooks.
[Contributing](../CONTRIBUTING.md) is the reference for the rules. This page is the order.

## 1. Decide it belongs, in an issue

Anything larger than a fix starts as an issue, before any code. The hard part of this program
is almost never how to write something, it is what the thing should do when the input is
strange, and that argument is cheaper to have in an issue than in a diff.

Answer three questions in it:

* **What does the user see?** Say it as a sentence a user would say.
* **What happens in the awkward case?** The empty case, the old data, the very long value.
* **Does it need a setting?** Usually not. A setting is a cost every reader of the Settings tab
  pays forever, so the default has to be wrong for enough people to be worth one.

> **In the example.** The feature is one sentence: the script list should say which voice each
> script is saved with. The awkward case is the script with no voice recorded, either because
> it predates the feature that records one or because somebody dropped a `.txt` into the folder
> by hand. That case is answered before any code: it reads "Not saved", not an empty cell, so
> the column never looks broken. No setting, since nobody wants this column hidden.

## 2. Pick the layer

One rule decides it. **Logic goes in `TtsUtil.Core`, and Core stays free of Windows.** Core is
the half that could run somewhere other than Windows one day.

| The work is | It goes in |
| --- | --- |
| Text, chunking, filters, silence, aliases, wave IO, voice discovery, settings, the manifest | `TtsUtil.Core` |
| A window, a control, playback, Windows speech, OCR, a file dialog | `TtsUtil.App` |
| A platform service Core needs | An interface in Core, implemented in App, the way `IPageImageOcr` is |

A new area of behavior in the app is a **new partial file** of `MainWindow`, not more lines in
`MainWindow.xaml.cs`. One file per area: `MainWindow.Documents.cs`, `MainWindow.Updates.cs`.

> **In the example.** Nothing new in Core. The voice was already saved beside the script by an
> earlier feature, in `ScriptProperties` with its `ScriptLibrary.LoadProperties` reader, so this
> is purely a matter of showing what Core already knows. No new partial file either: the script
> list already lives in `MainWindow.xaml.cs`, and a column on an existing list is not a new area.
>
> Notice what that means about ordering. The storage landed in its own earlier commit, and this
> one is only the display. A feature that needs both is two commits, storage first, so each one
> reads on its own.

## 3. Read the neighbors before writing a line

Find the two or three places that already do the closest thing and match them. In this codebase
that means the naming (`SizeTabs`, never `TabLayoutManager`, and no `Helper`, `Manager` or
`Util` classes), the license header at the top of every file, UTF-8 with CRLF endings, and
comments that say what rather than why and stop at two lines. Reasoning belongs in the commit
message, not above the code.

> **In the example.** The neighbors are `ScriptRow`, the class each row in the list is bound to,
> and `RefreshScripts`, the one method that rebuilds the list. Both already existed and both are
> small, which is the signal that the feature fits where it is going.

## 4. Change the model first

Start at the data the screen will bind to, not at the screen. If the model shape is right, the
markup is a few lines, and if it is wrong no amount of markup saves it.

Give the awkward case from step 1 a name here, so the test and the markup can both refer to it
rather than repeating a magic string.

> **In the example**, `ScriptRow` gains one property and one constant:
>
> ```csharp
> public ScriptRow(SavedScript script, string? voice = null)
> {
>     Script = script;
>     Voice = string.IsNullOrWhiteSpace(voice) ? NoVoice : voice;
> }
>
> /// <summary>What the voice column shows for a script saved before voices were kept.</summary>
> public const string NoVoice = "Not saved";
>
> /// <summary>The voice this script was last read with.</summary>
> public string Voice { get; }
> ```
>
> Three decisions worth copying. The fallback is applied **in the constructor**, so no caller
> can produce a row with a blank voice. `NoVoice` is a `const`, so the test asserts against the
> same string the screen shows and a reworded fallback cannot quietly break the test. And the
> new parameter is optional, so every existing caller still compiles, which keeps the diff to
> the feature rather than to a signature change.

## 5. Feed the model from the one place that builds it

Find the single method that populates the thing and change it there. If there is more than one
such place, that is the refactor to do first, in its own commit.

> **In the example**, `RefreshScripts` is that place, and the change is two lines:
>
> ```csharp
> var voice = Scripts.LoadProperties(script.Title)?.VoiceName;
> ScriptList.Items.Add(new ScriptRow(script, voice) { Chosen = ticked.Contains(script.Title) });
> ```
>
> `LoadProperties` already returns null for a script with no properties file, and `ScriptRow`
> already turns null into "Not saved", so the awkward case needs no branch here. That is the
> payoff for deciding it in step 1 and encoding it in step 4.

## 6. Put it on screen

The markup comes last of the three, and it carries the conventions the tests and the screen
reader depend on:

* **`x:Name` anything a test will touch.** `[assembly: InternalsVisibleTo("TtsUtil.App.Tests")]`
  is what lets a test reach a named control, so a control without a name is a control no test
  can press.
* **`AutomationProperties.Name` on anything with no visible label**, a bare icon button above
  all.
* **A `ToolTip` on anything that can be trimmed**, so the full value is still reachable.
* **A column in a list is two edits, not one:** the header and the row template, with the same
  width and the same margin. They are separate elements, and nothing checks that they line up
  except your eye.

> **In the example**, a header row above the list:
>
> ```xml
> <DockPanel DockPanel.Dock="Top" Margin="26,0,2,2">
>     <TextBlock DockPanel.Dock="Right" Text="Voice" Width="96" Margin="8,0,0,0"
>                FontWeight="SemiBold" Foreground="#555" />
>     <TextBlock Text="Script" FontWeight="SemiBold" Foreground="#555" />
> </DockPanel>
> ```
>
> and the matching cell in the row template:
>
> ```xml
> <TextBlock DockPanel.Dock="Right" Text="{Binding Voice}" Width="96"
>            Margin="8,0,0,0" Foreground="#555"
>            TextTrimming="CharacterEllipsis"
>            ToolTip="{Binding Voice}" />
> ```
>
> `Width="96"` and `Margin="8,0,0,0"` appear in both, which is what makes the column a column.
> The `26` in the header margin clears the tick box the rows carry. Long voice names trim with
> an ellipsis and keep the whole name in the tooltip, which is the alternative to letting one
> long name push the script title off the row.

## 7. Run it before you test it

```powershell
.\scripts\DebugRun.ps1
```

That builds into a folder under `%TEMP%` with a `portable.txt` marker beside it, so the session
uses its own settings, drafts and script library and never touches the copy you use every day.
The folder goes when the window closes.

Look at the awkward case on screen, not just the happy one. Make the value long, make it empty,
make the window narrow.

> **In the example**, that means a script with a voice, a script without one, and a voice name
> long enough to trim, at a window width that squeezes the list.

## 8. Write the test that would fail

**The standing rule: name the change that would make this test fail.** If the answer is nothing
a person would plausibly write, the test is not worth its maintenance and it will be deleted
when somebody finds it. A test that a constructor assigned its argument is not a test.

Prefer the real thing. The App tests build an actual `MainWindow` on an STA dispatcher thread
and raise real Click events, so a test can press a button rather than call the handler behind
it. Where a feature has a pure part and a layout part, factor the pure part out and test that:
`MainWindow.TabWidth(room, count)` exists in exactly that shape.

Cover the awkward case in the same test rather than writing a second one for it. Two asserts in
one test beat two tests with identical setup.

> **In the example**, one test covers both cases:
>
> ```csharp
> [Fact]
> public void TheListShowsTheVoiceEachScriptWasSavedWith()
> {
>     File.WriteAllText(Path.Combine(_scriptsDir, "With a voice.txt"), "First.");
>     File.WriteAllText(Path.Combine(_scriptsDir, "Without one.txt"), "Second.");
>     new ScriptLibrary(_scriptsDir).SaveProperties("With a voice", new ScriptProperties { VoiceName = "David" });
>
>     var window = CreateWindow();
>     _wpf.Invoke(() =>
>     {
>         window.RefreshScripts();
>         var rows = window.ScriptList.Items.Cast<ScriptRow>().ToDictionary(r => r.Title, r => r.Voice);
>
>         Assert.Equal("David", rows["With a voice"]);
>         Assert.Equal(ScriptRow.NoVoice, rows["Without one"]);
>     });
> }
> ```
>
> It fails if the plumbing in step 5 is dropped, if the fallback in step 4 is dropped, or if
> either stops reaching the rows the list is built from, which are the three things a person
> would plausibly break. It writes real files into the per test temporary scripts folder the
> fixture makes and deletes, so it needs no mock and leaves nothing behind. It asserts against
> `ScriptRow.NoVoice` rather than the literal `"Not saved"`, so rewording the fallback is a one
> line change rather than a failing test.

## 9. Write the documentation in the same commit

Code without its documentation is not finished, and the pieces live in different places
depending on what changed.

| What you added | Where it is written down |
| --- | --- |
| Anything a user can see | The relevant section of `README.md`, and [usage.md](usage.md) if the tab has a section there |
| A setting | `SettingsHelp` in Core, which is the question mark text. A setting with no help text is not finished |
| A downloadable voice | Three places on purpose: `DownloadableVoices`, `installer\TtsUtilWin.iss`, `scripts\FetchVoices.ps1` |
| A dependency | `THIRD-PARTY-NOTICES.txt`, and it has to be Apache 2.0 compatible |
| Anything a user can see, again | A bullet under `## Unreleased` in `distribution\CHANGELOG.md` |

The changelog is an embedded resource that the Updates tab reads, so every `## ` heading becomes
a release in that list and the format matters.

> **In the example**, the README gained a paragraph in the scripts section, saying what the
> column is, what "Not saved" means and that the voice lives in a small `.voice.json` beside the
> text. The test badge went from 455 to 456 in the same commit.
>
> **What the example got wrong.** The changelog bullet is not in this commit. It arrived later
> in `d9208eb docs: log the unreleased changes`, which swept up several features at once. That
> works, and it is a real habit in this repository, but it relies on remembering. Writing the
> bullet in the feature commit is the version to copy.

## 10. Let the hooks check it

```powershell
git add -A
git commit -m "feat(scripts): show the saved voice as a column in the script list"
```

`pre-commit` takes about eight seconds and runs trailing whitespace and conflict markers,
staged file sizes, staged build output, `dotnet format --verify-no-changes`, a build with
warnings as errors, and the Core tests. It also compiles and runs the installer version tests
when an `.iss` file is staged. It skips the last four when the commit stages no code, so a
documentation commit is instant.

`commit-msg` checks the message:

```
type(scope): summary in lowercase, imperative, no trailing period
```

* Types: `feat`, `fix`, `docs`, `test`, `refactor`, `perf`, `build`, `ci`, `chore`, `style`,
  `revert`. The scope is optional and lowercase.
* Subject of 72 characters or fewer, not starting with a capital.
* Body, if any, of **200 characters or fewer**.
* No tool attribution lines.

**One commit per idea.** A refactor and the feature it enables are two commits, which is what
makes the feature readable a year later.

> **In the example:**
>
> ```
> feat(scripts): show the saved voice as a column in the script list
>
> The script list gains a Voice column reading each script's saved properties, with a header
> row and a 'Not saved' fallback for scripts with no voice recorded.
> ```
>
> The subject says what the user gets. The body names the three parts a reviewer should look
> for, in 157 characters.

## 11. Push, and let the whole suite run

```powershell
git push
```

`pre-push` runs both suites, the UI tests included, in about fifteen seconds. They sit on push
rather than on commit so that committing stays quick.

Run `.\scripts\InstallHooks.ps1` once per clone and `core.hooksPath` points at `.githooks` for
good, which is what makes the plain `git commit` and `git push` above do all of this. Bypass a
single run with `--no-verify` and a whole session with `$env:SKIP_HOOKS = 1`, and expect to be
asked why.

For a change sent as a pull request: branch from `master`, keep the branch to one subject, say
what a reviewer should try by hand, and say explicitly when something was verified against a
real voice model, because the suite cannot do that. Leave `Directory.Build.props` alone.

## 12. It ships with the next release

Nothing else is required of the feature author. The release step renames `## Unreleased` to the
version going out and bumps `Directory.Build.props`, and [releasing.md](releasing.md) covers
the rest.

## The short version

1. Issue first, and answer the awkward case in it.
2. Core for logic, App for Windows, a new partial file for a new area.
3. Read the neighbors and match them.
4. Model first, with the awkward case named as a constant.
5. Feed it from the one method that builds it.
6. Markup last, with `x:Name`, an automation name, a tooltip, and a header matching its column.
7. `DebugRun.ps1`, and look at the awkward case on screen.
8. One test, named for the change that would break it, asserting against the constant.
9. README, usage, settings help, changelog, in the same commit.
10. Conventional Commits, one idea per commit, and let `pre-commit` argue with you.
11. Push and let the full suite run.
