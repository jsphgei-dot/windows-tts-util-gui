[Documentation index](index.md) | [Usage](usage.md) | [Performance](performance.md) | [Build](building.md) | [Release](releasing.md) | [Develop](developing.md) | [Write a feature](writing-a-feature.md)

---

# 🎧 Usage

## Voices

The program can speak the moment it opens, on the three Microsoft voices Windows installs
itself. Downloading a model is a choice about quality, never a requirement.

The **Voices** tab lists the curated models with their size, license and whether they are
installed. Tick the ones you want and press **Install**: they are downloaded, checked and
unpacked one at a time, with a progress bar counting the batch and a Cancel button that stops
the current download and leaves the rest alone. With nothing ticked, Install takes the selected
row. Clicking anywhere on a row moves its tick, here and on the Scripts tab, and holding Shift
carries that tick across every row back to the last one you clicked. **Remove** deletes
an installed voice again, and a canceled or failed
install leaves nothing behind. When the configured voices directory cannot be written, which
happens for an all users installation under Program Files, the download goes to
`%LOCALAPPDATA%\TtsUtilWin\voices` instead and the tab says so.

Voices from elsewhere can be brought in too. Paste an https link to a voice archive in the box
under the list and press **Import voice library**: the archive is downloaded, unpacked beside the
other voices and picked up straight away, with the same progress bar and Cancel button. Links
ending `.tar.bz2`, `.tar.gz`, `.tar.xz`, `.tar`, `.tgz` and `.zip` are understood. If nothing in
the archive looks like a voice model, nothing is kept. The **?** beside the button lists the
repositories where those links are found, and is a reminder to read each voice's license before
publishing or selling what it reads.

The voice picker along the top lists downloaded voices in the order they arrived, oldest
first, so a voice installed just now sits at the bottom of that group. The Windows voices,
Microsoft David, Microsoft Zira and Microsoft Mark, follow them whenever **Also offer the voices
Windows already has** is on, and need no download of any kind.

The three defaults were chosen for clear, free licenses:

| Model | Voice | License |
| --- | --- | --- |
| `vits-piper-en_US-ljspeech-high` | English (US), single speaker | LJ Speech data set, public domain |
| `vits-piper-en_US-libritts_r-medium` | English (US), 900+ speakers | LibriTTS-R, CC BY 4.0 |
| `kokoro-en-v0_19` | English, 11 voices | Apache 2.0 |

The installer offers these three plus `vits-piper-en_GB-alan-medium`,
`vits-piper-en_US-amy-low` and `vits-piper-en_US-lessac-medium`.

For a portable copy or an unattended setup, `scripts\FetchVoices.ps1` does the same job:

```powershell
.\scripts\FetchVoices.ps1 -List          # the whole catalog
.\scripts\FetchVoices.ps1 -Default       # the three defaults, about 490 MB
.\scripts\FetchVoices.ps1 -Name kokoro-en-v0_19
```

On an all users installation the program folder is under Program Files, so running the script
there needs an elevated PowerShell, or a `-Destination` you can write to. The authoritative
license for a model is the `LICENSE` or `MODEL_CARD` file inside its folder, and the About tab
shows it for the selected voice.

The voices directory is resolved in this order, first hit wins:

1. `--voices <path>` on the command line
2. the `TTSUTIL_VOICES` environment variable
3. the Settings tab, stored in `settings.json`
4. `%LOCALAPPDATA%\TtsUtilWin\voices`
5. a `voices` folder beside the executable

Press **Rescan** after adding a model by hand.

Right click a voice in the Voices tab for a menu with **Install**, **Uninstall**, **Go to web
location** and **Go to file location**. Whatever the voice cannot do is greyed out: **Uninstall**
and **Go to file location** for a voice that is not installed, **Install** for one that is, and
**Go to web location** for a voice with no known address. The first two do what the buttons under
the list do, the third opens the page the built in voices are published on, and the last opens the
voice's own folder in Explorer.

A model dropped into the voices folder by hand is listed alongside the built in ones, marked
**Added by hand**, so it can be uninstalled or opened from the same menu. Nothing on the web is
known for one of those, so **Go to web location** is greyed out for it.

**Star** beside the voice picker keeps a voice at the top of the list, the way it works for
speakers, and the stars are kept in `settings.json`. **Favorites** narrows the picker to the
starred voices, and shows everything again when pressed a second time. A voice hidden that way
is still the one a reading uses, and the status bar says so.

### Other languages

The Voices tab also lists Spanish (Spain and Mexico), French, German, Italian, Portuguese
(Brazil), Dutch, Polish, Swedish, Turkish, Vietnamese, Russian, Ukrainian, Greek, Arabic,
Hindi and Chinese. None is installed by default and none is offered as a setup checkbox: they
are there to install when you want one.

Anything outside the Latin alphabet, which is Russian, Ukrainian, Greek, Arabic, Hindi and
Chinese here, needs **Characters read aloud** set to any letter or digit in Settings,
otherwise the strict default removes the whole script before it reaches the voice. Installing
one of those says so. Vietnamese is Latin but heavily accented: under the strict setting its
accents fold away, so it wants the wider setting too.

### Speakers

A multi speaker voice shows a speaker row under the voice picker. Names come from the model's
own metadata where it has any: piper models carry a `speaker_id_map`, so
`vits-piper-en_US-libritts_r-medium` lists its 904 speakers by LibriTTS reader id rather than
by position. Where a model says nothing, the speakers are numbered.

To name them yourself, drop a `speakers.txt` into the voice folder. A line is either
`id = name` or a bare name, bare lines numbering themselves from zero, and blank lines and
lines starting with `#` are ignored. Your names win over the model's.

```
# voices/vits-piper-en_US-libritts_r-medium/speakers.txt
12  = Narrator
307 = Warm, low
```

The search box filters by name or by leading digits of the number, and Star keeps a speaker at
the top of the list. Both the chosen speaker and its starred speakers are remembered per voice.
Favorites shows only the starred speakers, and shows everything again when pressed a second
time.

## Writing and editing

The Text tab opens with a save icon at the left of the row above the text and a title box
beside it. Type a name, press the icon, and the text is written to the script library under
that name. **Ctrl+S** does the same, except that it asks for the name first in a small window,
filled in with the title already in the box. Saving under a name that exists replaces it. The
same title shows on the Scripts tab, so saving from either place uses one name.

Pressing anywhere that is not a control takes the cursor out of whatever box it was in, so no
caret is left blinking in a box that has been clicked away from.

The name belongs to the tab it was typed in. Switching tabs brings that tab's name back, and a
new tab starts with an empty box, so a fresh piece of text is never saved over the script the
last tab came from.

The icon answers for itself: it turns into a tick, green when the name is written for the first
time and blue when an existing script is replaced, and goes back to normal a moment later. The
status bar says which of the two happened as well.

The tab strip above the text stays on one line however many tabs are open, and the tabs share
it evenly the way a web browser's do. One tab on its own is 180 pixels wide, more tabs split the
room between them down to 80 pixels each, and names too long for the space end in an ellipsis
with the full name in the tooltip. Past that the strip scrolls sideways, by its scrollbar or the
mouse wheel over the strip, so closing one never reshuffles the rest onto different rows.

While the pointer is among the tabs they keep the width they have, so closing several in a row
does not move the next close button out from under the pointer. The widths are worked out again
once the pointer leaves the strip.

A tab whose text has moved on from the last save wears a **\*** after its name, so unsaved work
is visible without opening the tab. The star goes as soon as the text is saved as a script, and
a tab that comes from a script or a file starts clean. Closing a starred tab asks first, and
that question has a **Don't show this again** box for anyone who would rather it did not. The
same switch lives in Settings as **Ask before closing a tab with unsaved changes**, so it can be
turned back on later.

Under it is the editing toolbar. Scripts are plain text files, so there is no bold, no italic
and no color: everything here changes the words themselves and survives a round trip through
a `.txt` file.

| Tool | What it does |
| --- | --- |
| Undo, redo, cut, copy, paste | The usual, on the text box |
| Bulleted list, numbered list | Marks the selected lines, or every line. Pressing again takes the marks off |
| Indent, outdent | Four spaces on or off the front of each line |
| AA, aa, Aa, Ab | Upper case, lower case, sentence case, title case. Words already in capitals are left alone |
| Clean up | Join wrapped lines, collapse blank lines, tidy spacing |
| Find and replace | A strip under the toolbar, also on **Ctrl+H** |
| Size, Wrap | How the editor looks. Neither changes a saved file |

A tool with text selected works on whole lines, never half of one. With nothing selected it
works on everything. **Join wrapped lines** is the one to reach for after pasting from a PDF:
it puts a sentence split over four lines back together, and leaves paragraphs and list items
alone.

## Reading text

Type or paste into the Text tab and press **Read**. **Read as I type** speaks each word as you
finish it, never single letters. The voice, the speaker and the speech rate sit along the top.

The playback buttons carry icons rather than words: play, pause, stop, save audio, and an arrow
for **Read from here** above the line list. Resting the pointer on one names it, and screen
readers get the same name, so nothing is lost by dropping the captions.

## The media keys

The play, pause and stop keys on a keyboard work while the program is running, and the Windows
media overlay shows the script title with the same three buttons. They drive the reading that
is already in flight, so pause from the keyboard is the same pause as the button. On a machine
where Windows will not hand out the transport controls, the program carries on without them and
says nothing.

## Moving about while it reads

Pause holds the audio device and Resume carries on from the same place. Synthesis keeps a few
seconds of lookahead and then waits, so pausing does not quietly race ahead generating the rest
of the book. Stop gives up on the run entirely; pressing it while paused releases the pause
first, so nothing is left waiting. Stop also remembers the line it cut off on, and a **Carry
on** button appears beside it that starts the reading again there rather than at the top. It
goes away once the reading is picked up, or when you move to another document tab.

**Pause when I click away**, beside **Read as I type**, holds the reading whenever another
window takes over. It is off to begin with. Press Resume to carry on.

The voice, the speaker, the speed and everything on the Voices tab are held still while
something is playing. They are grayed out, and resting the pointer on one says to stop the
playback before changing it. Changing a voice out from under a reading in flight is what
that prevents.

There is no scrub bar, because the audio does not exist yet: it is synthesized as it plays.
Moving about is done by line instead, and Stop is no part of it. While a reading is in flight,
**Read** turns into **Restart** and starts the same reading again from where it began. **Read
from here**, which sits above the line list, and a double click in the line list, move the
reading to that line without stopping first. Each of these stops the current run, waits for it to unwind, and starts the next one.

A restart also sounds the same as the reading it replaces. The voice draws its prosody from a
random generator that runs on from one sentence to the next, so reading a passage twice with
the same voice gives two different deliveries. Every run therefore begins by winding the voice
back to the state it loaded in, which takes about three quarters of a second on a Piper medium
model and is reported as *Preparing the voice*. The same applies to a repeat and to each entry
in the queue: a passage read twice is read identically.

The Text tab shows a numbered line list beside the editor. Double click a line, or select it
and press **Read from here**, to start there instead of at the top. With nothing selected,
**Read from here** uses the line the cursor is on, so you can click into the text and carry on
from there, whether or not something is already playing.

The list follows what you are hearing, not what has been synthesized: audio is generated
several seconds ahead of the speaker, so following synthesis left the highlight a line or two
early. The reading is marked as it is queued and the list follows the marks as they play. That
makes Stop and **Read from here** a resume, since the selection is left on the line playback
reached. Untick **Lines** to hide the list and give the editor the full width.

The editor follows along too. While a reading plays, the text scrolls so the line being heard
sits in the middle of the box, whether or not the list is showing, and the cursor and any
selection are left where they were. Writing to a file does not scroll the text.

## Files and PDFs

The File tab takes a text file or a PDF. **Import to Text tab** puts the extracted text in the
Text tab so you can read it over and edit it before listening; **Read file** and **Convert to
WAV** go straight through.

Text comes from the PDF's own text layer, read with PdfPig, which covers anything produced by a
word processor or a typesetter. A page with no text layer is a scan, and its images are passed
to the OCR engine built into Windows, so nothing is downloaded and nothing leaves the machine.
A file is judged by its header rather than its extension, so a text file named `.pdf` is still
read as text.

The status line reports how many pages had no text layer and which pages yielded nothing, so a
partial read is visible rather than silent. If Windows has no OCR language pack installed,
scanned pages are reported as unreadable instead of failing.

The **?** button beside Browse spells out which files can be read in and what happens to them.

Several files can come in at one go. **Open files in tabs** gives each chosen file its own tab
on the Text tab, and **Save files as scripts** puts each one straight in the script library.
Either way the name comes from the file, so `Chapter one.pdf` becomes `Chapter one`. Files
opened in tabs stay out of the script library unless **Opening in tabs also saves them as
scripts** is ticked, which is off to begin with. They are
read one after another, **Stop** gives up on the rest, and anything that yielded no text is
named in the line under the buttons rather than passed over in silence.

## Saving audio

The save icon sits beside Stop under the text, and **Ctrl+Shift+S** does the same thing from
the keyboard: both ask where the file should go and then write it in the background.

Save audio writes MP3 by default, which is roughly a tenth the size of the equivalent wave file
and plays in anything. Switch to WAV in Settings if you want uncompressed audio, and set the
MP3 bit rate there too. The file type actually written follows the extension you choose in the
save dialog, so you can override the default for one file.

Files go to a `TTS Util` folder inside Music unless you set another output directory. The
folder is created when it is first needed. After a file is written the status bar names it and
the folder is a link: click it to open that folder.

A long file can take a while, so **When a file finishes** in Settings decides how the program
says it is done: no notification, a sound, or a Windows notification naming the file alongside
the sound. A sound is the default. Converting several scripts at once announces itself once at
the end rather than once per file.

MP3 is encoded with the Media Foundation encoder built into Windows, so nothing extra is
installed. Encoding happens after synthesis, on the same background thread, so the window stays
responsive; the status bar says "Encoding MP3..." while it runs.

## Scripts

The Scripts tab keeps named texts you can come back to. Type a title, press **Save from Text
tab**, and the text is written as a `.txt` file; **Open in Text tab** loads one back, ready to
read. Double clicking a script opens it in a new tab instead, leaving what you already had open
alone, and the Text tab wears a red **!** until you look at it. Rename and Delete do what they
say, and Delete asks first.

Scripts are plain UTF-8 text files, one per script, named after the title. They live in
`%APPDATA%\TtsUtilWin\scripts`, or in a `scripts` folder beside the executable on a portable
copy. **Open folder** opens that location. Nothing is trapped in an application format: edit
the files in any editor, or drop one in from elsewhere and it appears in the list.

Characters Windows will not accept in a file name become spaces, so `Chapter 1: the beginning`
is stored as `Chapter 1 the beginning.txt`.

The list has a **Voice** column showing the voice each script was saved with, so you can see at
a glance what a script will be read in. A script saved before its voice was recorded, or one
dropped into the folder by hand, reads "Not saved" until you save it from the Text tab again.
The voice sits in a small `.voice.json` file beside the text, which stays a plain text file.

Each script in the list has a tick box. Tick the ones you want, press **Convert ticked to
audio**, choose a folder, and each is written as its own file in the format set in Settings.
They are written one at a time; **Stop** gives up on the rest. A name already in use gains a
number rather than writing over the file that is there. **Tick all** and **Clear** work the
list in one go.

## The queue

The panel on the right of the Scripts tab is a playlist. The **+** button adds whatever is
selected in the script list, or the Text tab itself when nothing is selected, so a chapter you
are drafting can sit in a run of saved ones. The arrows reorder, the bin removes the selected
entry and the broom empties the queue. The play button starts from the top, a double click on
an entry starts from there, and the highlight moves down the list as the reading moves on.

The repeat button cycles three ways and the label beside it says which is in force:

| Mode | What happens at the end of an entry |
| --- | --- |
| Off | the queue moves on, and stops after the last entry |
| Repeat one | the same entry is read again, for as long as you leave it |
| Repeat all | the queue moves on, and starts again from the top after the last entry |

The same button sits beside the media buttons under the text, so a reading can be set looping
without leaving the Text tab. Both show the mode that is on, dimmed when it is off.

Repeat one works on a single reading as much as on the queue, so the Text tab can be left
looping without pressing Read each time. Stop ends the repeat, whichever mode is set, and the
mode is remembered between sessions. An entry that cannot be read (an empty script, a file that
has gone) is reported and skipped rather than retried for ever.

## Aliases

The **Aliases** tab holds the rules that tell the voice to say one thing where the text says
another. Type the word to look for, what to say instead, and press Add. Rules run top down,
whole word and case matching are per rule, and what one rule writes is left alone by the rules
under it. The try it box shows the effect on a line of your own. Lists import and export as
JSON, so they can be shared.

Four lists come with the program, and none of them is on until it is ticked:

| List | What it reads |
| --- | --- |
| Chemistry | Element symbols and common formulas, so `K` is potassium, `Ca` is calcium and `H2O` is water |
| Math symbols | The signs a voice otherwise skips, such as the root sign, pi, and less than or equal to |
| Units and measures | The short forms of everyday units, from kilometers to megabytes |
| Everyday shorthand | The abbreviations prose is full of, such as e.g., i.e. and etc. |

Ticking a list puts its rules straight into the grid, so every one of them can be read, edited,
reordered or turned off like a rule of your own. Unticking takes back the rules that are still
as the list wrote them, and leaves behind every rule you changed, now marked as one of your own.
Each ticked list gets its own tab above the grid, next to **My rules**, so a list can be looked
through without scrolling past the others.

The rules on show can be kept under a name of your own. Type a name in the box under **Your
saved rulesets** and press the save icon: the rules on the current tab are written to
`rulesets\<name>.json` beside the settings, and the name appears as a tick box beside the lists
that ship. Ticking it puts those rules back in the grid under a tab of their own, unticking
takes them out again, and the bin icon forgets the ruleset. A ruleset is an ordinary alias
file, so it can be exported and shared like any other.

Symbols that are also ordinary English words, among them `In`, `No`, `He` and `As`, arrive
turned off inside the chemistry list, so a sentence starting with one of them is read as
English.

A rule that can never fire is shown in red in the grid and named in orange under it, either
because the same word is listed twice or because a rule above it matches inside it and takes
the text first. Hovering a red row says which rule stands in its way. The lists
that ship are checked for the same thing by a test, so ticking one never quietly does nothing.

## Settings

The tab is laid out in seven groups, each under a heading with a rule beneath it: **Reading**,
**Pauses**, **What gets read**, **Audio files**, **Voices**, **Performance** and **Window**.
Related settings sit together, so the speech rate and the repeat mode are found in one place and
the two folders are not a dozen rows apart. **Apply**, **Reset to defaults** and **Reset aliases**
stay at the foot of the tab, below every group. Update settings have their own tab.

Every setting that needs explaining has a question mark beside it. Press one and a panel opens
with what the setting does, the range it accepts, the default, and an example where an example
helps. Press elsewhere to close it.

The panels cover the silence values, scaling silence to the speech rate, the hash and link
filters, both character settings, the two folders, threads, chunk length, the saved audio
format and the MP3 bit rate. The text lives in `SettingsHelp` in Core rather than in the XAML,
so it is readable and testable without opening the window. Every control on the tab also
carries a one line description on hover.

**Synthesis threads**, under **Performance**, is empty by default, which means the program
works the count out from the processors the machine reports. [Speed and machine
requirements](performance.md) has the rule, the measured render speeds behind it, and why
the Windows voices ignore the setting entirely.

Settings that also live elsewhere in the window appear here too, in whichever group they
belong to, so everything can be found in one place: **Read as I type**, **Pause when I click
away**, the alias switch, the reading speed and the repeat mode under **Reading**, the editor
text size and the question asked before a tab with unsaved changes closes under **Window**. A
change in either place moves the other. **Reset to defaults** puts every setting back to a first run,
after asking. Scripts, aliases and saved audio are left alone. **Reset aliases** empties the
alias list, after asking and after copying the list to `aliases.json.bak` beside it, so a list
built up over months is never lost to one press.

**Close to the notification area instead of quitting** keeps the program running when the
window is closed. The window hides among the hidden icons by the clock, and whatever it was
doing carries on, so a long reading finishes and a file being written is not cut off. Double
click the icon to bring the window back, or use its menu, which has **Open** and **Quit**.
Quitting from that menu ends the program the way closing the window normally would. The
setting is off to begin with, and takes effect once **Save settings** is pressed.

Two groups are worth knowing about before you go looking. **Silence** sets how long a pause
follows a line ending, a sentence, a question and an exclamation, in milliseconds, and can be
scaled with the speech rate so a fast reading does not sit in long gaps. **Filters** drop hash
characters, web links and mailto links before they reach the voice, since a spoken URL is
noise rather than information.

A first run starts with settings tuned for listening: a speed of 0.9, pauses of 450 ms after a
line ending, 350 ms after a sentence, 500 ms after a question and 350 ms after an exclamation,
pauses scaled to the speech rate, and all three filters on. **Reset settings** puts these back.

### What gets read aloud

By default only letters, digits and the punctuation that phrases speech reach the engine.
Everything else is dropped, because a text to speech engine happily says "dollar", "percent"
and "hash" out loud where a reader would say nothing.

* **Kept and voiced:** `a-z`, `A-Z`, `0-9`. Accented Latin letters are folded to their base
  letter, so `café` is read as `cafe` rather than broken into `caf`.
* **Kept as phrasing:** `. , ; : ? !` and their fullwidth forms. The engine pauses on these
  rather than naming them.
* **Dropped:** everything else. A symbol between two words leaves a gap so the words do not run
  together, while an apostrophe inside a word simply vanishes, so `don't` stays one word.
  Sentence ending punctuation already became silence in the chunker before this runs.

Settings has two controls for it. **Characters read aloud** chooses between the strict set
above, any Unicode letter or digit, or no filtering at all. **Also read these characters** lets
extra characters through: type the characters themselves, one after another, with nothing
between them. There is no separator, so `%$&+` allows all four. A comma typed there means the
comma character rather than a separator, and spaces do nothing.

One consequence worth knowing: the strict default removes non Latin scripts entirely, so a
Chinese or Japanese voice reads nothing under it. If a run ends up with nothing to say, the
status bar says so and points at the setting rather than finishing silently.

## The status bar

The bar along the bottom shows one message at a time, which used to mean a finished run, a
filtered count, or an error vanished as soon as the next message arrived. **History** opens the
last 50 messages, newest first, as text that can be selected and copied, and that scrolls once
there is more of it than the panel holds. Repeats are collapsed, so a progress figure that
updates many times a second does not crowd out everything else.

## Updates

Each time it starts, the program reads one published file, `latest.json` in the release
repository, picks the download matching the architecture it is running as, and compares its
version code with the running build. Nothing is sent with the request: no identifier, no text, no list of voices, and
no request at all while the setting is off.

A new version found at startup waits on the Updates tab: the tab wears a red exclamation mark,
the version number is named, and what changed in it is listed there. Nothing interrupts what you
were doing. The box **Update prompt popup box on startup** turns the older behavior back on.
While it is off, no update dialog opens anywhere: **Check now** writes its answer on the status
line, and **Install update** treats the press itself as the answer.

**Install update** sits beside **Check now** and is grey until a check finds something newer.
Pressing it downloads and runs the new version, so the update can be taken at any time from the
tab itself rather than only from the popup.

What happens next when the dialog is asked for depends on how the program was installed. An
**installed** copy offers to download the setup program, checks it against the SHA256 published in the manifest, and hands
over to it, since setup already knows how to upgrade in place and keep settings and voices. A
**portable** copy is told where the release is and left to unpack it, because replacing a folder
it may be running from is not the program's business. Saying no to a version means that version,
not every version after it.

All of this lives on its own **Updates** tab, which holds the boxes **Look for a new version
when the program starts** and **Update prompt popup box on startup**, a **Check now** button
that looks straight away whatever the boxes say, an **Install update** button for what the last
check found, the version you are running, and when the last check ran. The mark stays there even
after an offer is turned down.

The same tab carries the change history, newest first, read from the changelog built into the
program rather than fetched, so it says what this build is and works with no network at all. When
a check finds something newer, what changed in that release is listed above it, taken from the
published manifest.
