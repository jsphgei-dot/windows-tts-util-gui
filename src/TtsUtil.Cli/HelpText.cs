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
  ttsutil say <text> -o <file>      speak the words given here
  ttsutil read <file> -o <file>     speak a .txt or .pdf
  ttsutil convert <file> -o <file>  the same as read, and -o is required
  ttsutil voices                    list the voices that are installed
  ttsutil --version                 the build this is
  ttsutil --help                    this

An output of - writes the audio to standard output, so it can be piped:
  ttsutil say ""hello there"" -o - | aplay -

Options:
  -o, --out <path>     wave file to write, or - for standard output
  -n, --voice <name>   which installed voice to use, matched on the name
  -s, --speaker <n>    speaker number, for the voices that carry several
  -r, --speed <n>      0.1 to 4.0, where 1.0 is the voice's own pace
  -t, --threads <n>    threads the model uses, 0 to work it out from the machine
  -d, --voices <dir>   where the voices live, instead of the usual folder
  -f, --filter <what>  latin, any or off, which characters are allowed to be said
  -q, --quiet          keep progress off standard error

Settings come from the same settings.json the window uses, and a switch given
here wins for this run without being written back.";
}
