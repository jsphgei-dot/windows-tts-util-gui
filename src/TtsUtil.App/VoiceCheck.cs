/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Diagnostics;
using System.IO;
using TtsUtil.Core.Settings;
using TtsUtil.Core.Tts;

namespace TtsUtil.App;

/// <summary>Tries a sherpa voice in a child copy of the program before this one loads it,
/// as sherpa ends the whole process on a model it cannot use.</summary>
internal static class VoiceCheck
{
    internal const string Argument = "--check-voice";

    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(2);

    private static readonly VoiceCheckLedger Ledger = new(Path.Combine(
        Path.GetDirectoryName(AppSettings.SettingsPath) ?? AppSettings.AppDirectory, "checked-voices.json"));

    /// <summary>Loads a sherpa voice, checking it first unless this model has passed before.</summary>
    public static ITtsEngine LoadSherpa(VoiceDescriptor voice, int threads)
    {
        if (!Ledger.HasPassed(voice))
        {
            Run(voice, threads);
            Ledger.RecordPass(voice);
        }

        return SherpaTtsEngine.Load(voice, threads);
    }

    /// <summary>Starts the child and throws with what it reported when it fails.</summary>
    private static void Run(VoiceDescriptor voice, int threads)
    {
        var program = Environment.ProcessPath;
        if (program is null) return;

        var start = new ProcessStartInfo(program)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        start.ArgumentList.Add(Argument);
        start.ArgumentList.Add(voice.Directory);
        start.ArgumentList.Add(threads.ToString());

        using var child = Process.Start(start)
            ?? throw new InvalidOperationException($"{voice.Name} could not be checked before loading.");

        var errors = child.StandardError.ReadToEndAsync();
        _ = child.StandardOutput.ReadToEndAsync();

        if (!child.WaitForExit(Patience))
        {
            child.Kill(entireProcessTree: true);
            throw new InvalidOperationException(
                $"{voice.Name} took more than {Patience.TotalMinutes:0} minutes to load, so it was not loaded.");
        }

        if (child.ExitCode == 0) return;

        var said = LastLine(errors.Result);
        throw new InvalidOperationException(
            $"{voice.Name} cannot be used: it failed to load in a test run, and loading it here would have " +
            $"closed the program.{(said.Length > 0 ? $" The voice engine said: {said}" : string.Empty)}");
    }

    /// <summary>The child's side: loads the voice in the folder given and speaks a few words.</summary>
    public static int RunChild(IReadOnlyList<string> args)
    {
        try
        {
            var voice = VoiceCatalog.TryLoad(args[1])
                ?? throw new InvalidOperationException($"No voice was found in \"{args[1]}\".");
            var threads = args.Count > 2 && int.TryParse(args[2], out var parsed) ? parsed : 1;

            using var engine = SherpaTtsEngine.Load(voice, threads);
            engine.Synthesize("Hello.", 0, 1f, _ => true, CancellationToken.None);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }

    /// <summary>The last line written, without sherpa's source file and line prefix.</summary>
    internal static string LastLine(string text)
    {
        var last = text.Split('\n').Select(line => line.Trim()).LastOrDefault(line => line.Length > 0);
        return last is null ? string.Empty : SourcePrefix.Replace(last, string.Empty);
    }

    private static readonly System.Text.RegularExpressions.Regex SourcePrefix =
        new(@"^\S+\.(cc|cpp|h):\w+:\d+\s+");
}
