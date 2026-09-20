/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

namespace TtsUtil.Cli;

/// <summary>What `ttsutil` prints when it is asked what it can do.</summary>
internal static class HelpText
{
    public const string Usage = @"ttsutil, neural text to speech on your own machine.

Usage:
  ttsutil say <text>                speak the words given here out loud
  ttsutil read <file>               speak a .txt or .pdf out loud
  ttsutil convert <file> -o <file>  write a file to audio instead of playing
  ttsutil voices                    list the voices that are installed
  ttsutil check                     what this machine is and what it can play
  ttsutil --version                 the build this is
  ttsutil --help                    this

Without -o the audio plays through ALSA, which PulseAudio and PipeWire both
answer to. With -o it is written to that file, as a WAV or, when the name ends
in .mp3, as an MP3. An output of - writes to standard output, to be piped:
  ttsutil say ""hello there"" -o - | aplay -

Options:
  -o, --out <path>     file to write, or - for standard output, or leave it
                       out to play the audio out loud
  -n, --voice <name>   which installed voice to use, matched on the name
  -s, --speaker <n>    speaker number, for the voices that carry several
  -r, --speed <n>      0.1 to 4.0, where 1.0 is the voice's own pace
  -t, --threads <n>    threads the model uses, 0 to work it out from the machine
  -d, --voices <dir>   where the voices live, instead of the usual folder
  -f, --filter <what>  latin, any or off, which characters are allowed to be said
  -c, --format <type>  wav or mp3, instead of guessing from the file name
  -b, --bitrate <n>    MP3 bit rate in kbps, 8 to 320
  -a, --device <name>  the ALSA device to play through, instead of the default
  -q, --quiet          keep progress off standard error

Settings come from the same settings.json the window uses, and a switch given
here wins for this run without being written back.

When a sound library is missing, ttsutil reads /etc/os-release to name the
package your distribution calls it by. Nothing is sent anywhere. Set
TTSUTIL_NO_SYSTEM_DETECT=1 to leave that file unread and get general advice.";
}
