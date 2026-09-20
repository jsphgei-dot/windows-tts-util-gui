/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using TtsUtil.Cli;

using var stopping = new CancellationTokenSource();

// The first Ctrl and C stops the run politely, and a second one is left to the runtime.
Console.CancelKeyPress += (_, e) =>
{
    if (stopping.IsCancellationRequested) return;

    e.Cancel = true;
    stopping.Cancel();
};

// Standard output carries audio when -o is a dash, so nothing else may be written to it.
return new CliRunner(Console.Out, Console.Error) { Cancellation = stopping.Token }.Run(args);
