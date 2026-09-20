/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using TtsUtil.Cli;

// Standard output carries audio when -o is a dash, so nothing else may be written to it.
return new CliRunner(Console.Out, Console.Error).Run(args);
