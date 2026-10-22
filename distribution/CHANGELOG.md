# Changelog

Newest first. Version codes are monotonic and never reused. The top section carries what is
built but not yet released, and the release step renames it to the version going out.

## Unreleased

* **Stop cancels the save of the tab you are on.** Pressing Stop on the Text tab while the tab
  in view is being saved to audio now cancels that save and deletes the part written, rather
  than leaving a cut off file behind. A reading elsewhere carries on.

## 0.13.1-beta (version code 21)

* **Clicking the line list's scroll arrows no longer starts readings.** Two quick clicks on an
  arrow counted as a double click on the selected line, and a second click while the voice was
  getting ready started another reading that Stop could not reach. Only a double click on a
  line reads from it now, and a reading already starting turns the next one away.

* **Long lines wrap in the line list.** A line too wide for the list now wraps inside its own
  row, indented past its number, instead of running off the side behind a scroll bar. A faint
  rule marks where each line ends.

## 0.13.0-beta (version code 20)

* **The text follows the reading.** While a reading plays, the editor scrolls so the line being
  heard stays in the middle of the box, so you can read along. The cursor and selection stay
  put, and it works with the line list hidden.

* **New settings start tuned for listening.** A first run, or **Reset settings**, now reads at
  0.9 speed with pauses after lines, sentences, questions and exclamations (450, 350, 500 and
  350 ms, scaled to the rate) and with the hash, web link and mailto filters on. Settings
  already saved are left as they are.

* **The filtered count no longer counts spaces.** With sentence pauses on, the space starting
  each later sentence was reported as a filtered character, so a plain reading ended with
  "N character(s) filtered".

* **A voice that cannot load no longer takes the program down with it.** The voice engine ends
  the whole program when it is handed a model it cannot use, and the voice was remembered as
  the last one picked before it loaded, so every launch after that crashed the same way. Each
  voice is now tried once in a hidden copy of the program before it is loaded for real, and a
  bad one is reported with the engine's own reason while the program carries on.

* **Kitten voices load.** A Kitten model ships a `voices.bin` like Kokoro, so it was taken for
  Kokoro and loaded wrongly. The kind is now read from the model itself.

* **Opening a script mid reading leaves the voice alone.** Opening a script saved with another
  voice, in a new tab or the current one, switched the voice from under a reading or a file
  being written. The voice picker was already held still while something plays, and now the
  script's voice is too.

* **The update check knows what it is running on.** The manifest now names a download for each
  of x64, ARM64 and 32 bit, and a copy is offered the one it can actually run. An ARM64 or 32
  bit copy used to be offered the x64 installer, which was the only download the manifest could
  describe. A release with no build for an architecture points that copy at the release page
  instead of offering it anything.

* **Two architectures can no longer land side by side.** Installing the ARM64 or 32 bit build
  over an existing x64 one used to leave two copies fighting over one program folder, with two
  Start menu entries and two entries in Installed apps. Setup now finds the other copy wherever
  it registered itself, offers to remove it first, and ends if you say no.

* **The title box empties with the tab.** Closing the only open text tab cleared its words but
  left the name of the script sitting in the Title box, where the next thing typed could be
  saved over that script by accident.

* **The readme that ships with the program is about the program again.** Every 0.12.0-beta
  install and portable copy carried a readme whose opening line announced that the repository
  was retired, which was the old download repository's landing page rather than anything about
  running the program.

## 0.12.0-beta (version code 19)

* **One repository from here on.** The source repository is public, and it is now also where
  the downloads, the changelog and the issue tracker live. The separate distribution
  repository was always a temporary arrangement while the source was still private, and it is
  archived rather than deleted, so every build published there stays downloadable. This copy
  looks for its updates at the new address. An older copy is offered this release at the old
  one, and asks the new address afterward.
* **The program now runs on Windows 10 version 1607.** The floor was 1809, and the installer
  asked for no more than Windows 10 of any build, so it would install on versions the program
  then failed on. Both now say 1607, which is as low as the runtime goes. On builds before
  1709 the voices Windows already has read at their own speed, the only part of the program
  that old a Windows cannot do.
* **Synthesis threads now work themselves out from the processor.** The setting starts empty,
  and an empty setting means half the processors this machine reports, at least one and at most
  four. A machine with 32 processors or more allows eight, and the 32 bit build doubles whichever
  of those applies. Typing a number from 1 to 16 still overrides it. The old fixed default of two
  left most machines slower than they needed to be.
* **Every setting explains itself on hover.** The controls on the Settings tab carry the same
  one line descriptions the rest of the window has. The question marks beside them still open
  the longer explanation.
* **The program now runs on .NET 10.** The runtime it is built on moved from .NET 6, which
  reached its end of support, to the current long term support release. Nothing changes in
  what the program does. The download is about 5 MB larger.
* **Builds for ARM64 and 32 bit Windows.** Alongside the x64 build there are now
  native ARM64 and x86 ones, each with its own installer and its own speech engine rather
  than an emulated one. They are downloads from the release page; the in app update check
  still offers the x64 build.
* **The text tabs size themselves like a browser's.** One tab on its own is 180 pixels wide,
  more tabs share the strip evenly down to 80 pixels each, and a name too long for the space
  ends in an ellipsis with the whole name on hover. The name sits at the left of its tab and
  the cross at the right edge, at every width, rather than the pair sitting adrift in the
  middle of a wide tab. Past that the strip scrolls as before. A new tab takes its
  share straight away, and the widths then hold while the pointer is anywhere on the tab row, so
  closing several in a row does not move the next cross out from under it. They catch up the moment
  the pointer leaves that row.
* **The save icon moved to the left** of the row above the text, ahead of the title box, rather
  than sitting at the far right of the window.
* **The script list says what voice each script uses.** A **Voice** column beside the title
  shows the voice a script was saved with. Scripts saved before the voice was recorded, or
  dropped into the folder by hand, read **Not saved** until they are saved again.

## 0.11.0-beta (version code 18)

* **Close to the notification area.** A setting keeps the program running when the window is
  closed. It hides among the hidden icons by the clock and carries on reading or writing, and
  its menu has **Open** and **Quit**.
* **Star a voice.** Voices can be starred like speakers, which keeps them at the top of the
  picker, and **Favorites** narrows the picker to the starred ones.
* **Right click a voice** in the Voices tab for **Install**, **Uninstall**, **Go to web
  location** and **Go to file location**. Voices dropped into the folder by hand are now listed
  too, so they can be uninstalled or opened from the same menu.
* **The settings are grouped.** The tab is laid out under **Reading**, **Pauses**, **What gets
  read**, **Audio files**, **Voices**, **Performance** and **Window**, with Apply and the resets
  below every group.
* **Pressing outside a box drops the caret**, so no cursor is left blinking in a box that has
  been clicked away from.

## 0.10.1-beta (version code 17)

* **The title box belongs to the tab in front.** Switching tabs brings that tab's name back, and
  a new tab starts with an empty box, so fresh text is never saved over the script the last tab
  came from.
* **Repeat sits under the text as well.** The repeat button now appears beside the media buttons
  on the Text tab, showing the same mode as the one on the queue.

## 0.10.0-beta (version code 16)

* **Several files at once.** The File tab can open a pile of PDFs or text files as one tab each,
  or save them all as scripts, named after the files they came from.
* **Unsaved tabs say so.** A tab whose text has moved on from its last save wears a star, and
  closing one asks first, with a **Don't show this again** box and a matching setting.
* **Voices from anywhere.** Paste an https link to a voice archive and **Import voice library**
  downloads and unpacks it beside the rest. The **?** beside it lists where those links live.
* **Double click a script** to open it in a new tab, leaving what you had open alone.
* **The wheel no longer sticks, or crashes.** A list or text box that has nothing left to scroll
  hands the wheel to the page behind it.
* **The File tab explains itself.** A **?** beside Browse spells out which files can be read in
  and what happens to them.

## 0.9.2 (version code 15)

* **Ctrl+Shift+S writes the audio.** The save icon under the text keeps working as it did, and
  the keyboard now reaches the same thing.
* **The update box turns off every popup.** With **Update prompt popup box on startup** left
  unticked no update dialog opens anywhere: **Check now** answers on the status line, and
  **Install update** treats the press as the answer.
* **The Name this script window fits its buttons.** It grows to its contents and can be resized.
* **The Aliases tab keeps its grid.** The list of rules holds a usable height and the tab
  scrolls when the window is too short for everything on it.

## 0.9.1 (version code 14)

* **The installer says the Windows voices are already there.** Microsoft David, Microsoft Zira
  and Microsoft Mark sit at the top of the components page as ticked, greyed out rows, and the
  page says in words that no voice model has to be downloaded at all. Both READMEs say the same.
* **The plus on the Text tab stays where it was.** Adding a tab walks the strip to its right
  end, so the plus can be pressed again without scrolling the headers by hand.
* **One sound for a finished file, not two.** The notification setting played the Windows
  asterisk on top of the sound Windows plays for the popup itself, and documents written side by
  side made a sound each.

## 0.9.0-beta (version code 13)

* **Alias rules save as a ruleset of your own.** Name the rules on show and press save: the name
  appears as a tick box beside the lists that ship, puts those rules back in the grid under a tab
  of its own, and the bin icon forgets it again.
* **A rule you changed stays yours.** Unticking a list takes back only the rules still as the
  list wrote them, so an edit is never thrown away. The old Make their rules mine button is gone,
  since nothing needs it now.
* **Rules that can never fire show in red.** The clashing row is colored in the grid and says on
  hover which rule above it stands in the way.
* **Reset aliases.** A button on the Settings tab empties the alias list, after asking and after
  copying the old list to aliases.json.bak beside it.
* **Install update.** The Updates tab has a button for the version the last check found, grey
  until there is one, so the update can be taken from the tab rather than only from the popup.
* The startup box is now called Update prompt popup box on startup.

## 0.8.0-beta (version code 12)

* **The lists that ship are real rules now.** Ticking chemistry, math symbols, units or everyday
  shorthand puts those rules straight into the grid, each list under a tab of its own, so they
  can be read, reordered and edited like any other rule. Unticking takes them out again.
* **Rules that can never fire are named.** A rule an earlier one already covers is called out
  under the grid rather than quietly doing nothing.
* **A new version waits on the Updates tab.** Updates have a tab of their own, marked with a red
  exclamation while a newer release is out, naming the version and listing what changed in it.
  The old dialog at startup is now a box you can turn on.
* **The change history is in the program.** The Updates tab lists it newest first, built into
  the build, so it needs no network.
* **Voices install as a batch.** Tick as many voices as you want and Install works through them
  one at a time. A press anywhere on a row moves its tick, on the Voices and Scripts lists
  alike, and holding Shift carries that tick across a run of rows.
* Empty boxes throughout the program show in grey what they are for until they are typed in.
* The Text tab strip stays on one scrolling row instead of wrapping.

## 0.7.0-beta (version code 11)

* **Lists that ship with the program.** The Aliases tab now offers chemistry, math symbols,
  units and everyday shorthand, each off until it is ticked. The chemistry list reads K as
  potassium and H2O as water. Any of them can be copied into your own rules and edited there.
* **A finished file says so.** Settings decides whether writing audio ends with no notification,
  a sound, or a Windows notification naming the file alongside the sound. Converting several
  scripts announces itself once at the end.
* **Ctrl+S asks for the name.** Saving a script from the Text tab prompts for the title first,
  and the save icon shows a tick, green for a new script and blue when one was replaced.
* **Voices are listed in the order they arrived.** The downloaded voices sit oldest first, so a
  voice installed a moment ago is at the bottom rather than somewhere alphabetical.
* Settings from elsewhere in the program are repeated on the Settings tab, with a reset button,
  and the reading can hold itself when another window takes over.
* A second press on a button no longer runs the same command twice.

## 0.6.0-beta (version code 10)

* **Several texts at once.** The Text tab holds as many documents as you like, each on its own
  tab with a plus at the end of the strip and a cross to close one. Each keeps its own words and
  its own script name.
* **Audio is written in the background.** Writing one tab to a file leaves the others alone, so
  a second text can be typed, or read aloud, while the first is still rendering. The tab shows
  how far along it is.
* **Each script remembers its voice.** Saving a script keeps the voice, the speaker and the
  speaking speed with it, and opening or converting that script sets them back.
* Everything typed comes back when the program is opened again, every tab of it.

## 0.5.0-beta (version code 9)

* **The voices Windows already has.** Every Microsoft voice installed in Windows appears in the
  voice list beside the downloaded ones, so the program speaks before anything is downloaded.
  A setting turns them off again.
* **Aliases.** A list of words the voice should say differently, applied as the text is read.
  Whole word and case matching per rule, a preview box, and lists that import and export as
  JSON so they can be shared.
* **The message history is paged.** Each message has its own box, the strip below reads
  `< page 1/4 > >>`, and the page number can be typed. Settings hold how many messages are kept
  and how many a page shows.
* **Batch conversion.** Every saved script has a tick box. Tick what you want, press Convert
  ticked to audio, choose a folder, and each is written as its own file.
* **Saving no longer loses your place.** Writing audio to a file stops the reading, writes the
  file, then sets the reading up again at the line you were hearing, paused.

## 0.4.1-beta (version code 8)

* **A Check now button** beside the update setting in Settings. It looks straight away whether
  or not the daily check is on, reports that you are up to date when you are, and offers a
  version you turned down before.
* The Apply button no longer sits on top of the update checkbox in Settings.

## 0.4.0-beta (version code 7)

* **A script title above the text**, with a save icon beside it and **Ctrl+S**. The Scripts tab
  shows the same title, so saving from either place uses one name.
* **An editing toolbar.** Undo and redo, cut, copy and paste, bulleted and numbered lists,
  indent and outdent, upper, lower, sentence and title case, and find and replace on Ctrl+H.
  A tool with text selected works on whole lines; with nothing selected it works on everything.
* **Clean up.** Join wrapped lines, collapse blank lines and tidy spacing, for text pasted out
  of a PDF or an email.
* **Editor size and wrapping**, remembered between sessions. Neither changes a saved file.
* **The media keys work.** Play, pause and stop on a keyboard drive the reading, and the
  Windows media overlay shows the script title with the same three buttons.

Scripts are plain text files, so the toolbar carries no bold, italic or color: everything in
it changes the words themselves and survives being saved.

## 0.3.0-beta (version code 6)

First public release. Earlier versions were private alphas, so their downloads are not
published here.

* **A queue, and repeat.** Scripts can be lined up and played in order, with repeat off,
  repeat one, or repeat the whole queue.
* **Restarts sound the same.** The voice is wound back to the state it loaded in before every
  run, so a repeat is not a different performance.
* **Icons for playback.** Play, pause, stop, save and read from here, named on hover.
* **Read from here** moved beside the line list it works with.
* **A copyable status history.** The History panel is selectable, scrollable text.
* **Update checks.** Once a day, switchable off, with checksummed downloads.
* **Licensing.** `NOTICE` and `THIRD-PARTY-NOTICES.txt` ship with every build.
* **Fixed.** The bundled `FetchVoices.ps1` downloaded models one folder above where the program
  looks for them.

## 0.2.0-alpha (version code 5)

* Speaker names for multi speaker voices, with search, favorites and per voice memory.
* PDF import, reading the text layer and falling back to Windows OCR for scanned pages.
* A numbered line list, and reading from any line.
* A script library kept as plain text files.
* Pause and resume.
* MP3 output, saved under Music.
* A spoken character policy, and a status history.
* Fixed: the file progress bar understated progress on non ASCII files.

## 0.1.3-alpha (version code 4)

* Install voices from inside the program, with progress, Cancel and Remove.
* Downloads fall back to `%LOCALAPPDATA%` when the voices folder is read only.

## 0.1.2-alpha (version code 3)

* Setup recognizes an existing installation: upgrade in place, repair, and a downgrade warning.
* A running copy is closed through the Restart Manager rather than failing on a locked file.

## 0.1.1-alpha (version code 2)

* Setup can download voices during installation, with a size and a license shown for each.

## 0.1.0-alpha (version code 1)

* First tagged build: read typed text, a file or the clipboard, save to a wave file, read as
  you type, adjustable speed, and a voice picker.
