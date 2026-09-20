/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using TtsUtil.Core;
using TtsUtil.Core.Settings;
using TtsUtil.Core.Text;
using TtsUtil.Core.Tts;

namespace TtsUtil.Cli;

/// <summary>What the process exits with, so a shell script can tell the three apart.</summary>
internal static class ExitCodes
{
    public const int Ok = 0;

    public const int Failed = 1;

    public const int Usage = 2;
}

/// <summary>Reads a command line and carries it out. Everything it touches outside itself is a
/// property, so the tests can hand it a voice and a pair of writers.</summary>
internal sealed class CliRunner
{
    private readonly TextWriter _out;
    private readonly TextWriter _error;

    public CliRunner(TextWriter output, TextWriter error)
    {
        _out = output;
        _error = error;
    }

    /// <summary>Loads a voice at a thread count. Replaced in tests, which have no model to load.</summary>
    public Func<VoiceDescriptor, int, ITtsEngine> EngineLoader { get; set; } =
        (voice, threads) => SherpaTtsEngine.Load(voice, threads);

    /// <summary>Lists the voices in a folder.</summary>
    public Func<string, IReadOnlyList<VoiceDescriptor>> VoiceScanner { get; set; } = VoiceCatalog.Scan;

    /// <summary>The settings this run starts from, loaded from disk when nothing is set.</summary>
    public AppSettings? Settings { get; set; }

    /// <summary>Opens standard output for the audio when the output is a single dash.</summary>
    public Func<Stream> StandardOutputStream { get; set; } = Console.OpenStandardOutput;

    public int Run(IReadOnlyList<string> args)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            _error.WriteLine(options.Error);
            _error.WriteLine("Run ttsutil --help for what it takes.");
            return ExitCodes.Usage;
        }

        if (options.Version)
        {
            _out.WriteLine($"ttsutil {AppVersion.Display}");
            return ExitCodes.Ok;
        }

        if (options.Help || options.Command.Length == 0 || options.Command == "help")
        {
            _out.WriteLine(HelpText.Usage);
            return ExitCodes.Ok;
        }

        try
        {
            return Dispatch(options);
        }
        catch (Exception ex)
        {
            _error.WriteLine(ex.Message);
            return ExitCodes.Failed;
        }
    }

    private int Dispatch(CliOptions options) => options.Command switch
    {
        "voices" => ListVoices(options),
        "say" => Speak(options, FromArguments(options)),
        "read" => Speak(options, FromFile(options)),
        "convert" => Convert(options),
        _ => Unknown(options.Command),
    };

    private int Unknown(string command)
    {
        _error.WriteLine($"There is no {command} command.");
        _error.WriteLine("Run ttsutil --help for the ones there are.");
        return ExitCodes.Usage;
    }

    private int ListVoices(CliOptions options)
    {
        var directory = VoicesDirectory(options);
        var voices = VoiceScanner(directory);

        if (voices.Count == 0)
        {
            _error.WriteLine($"No voices in {directory}.");
            return ExitCodes.Failed;
        }

        foreach (var voice in voices)
        {
            var language = voice.Language.Length > 0 ? voice.Language : "unknown";
            _out.WriteLine($"{voice.Name}\t{language}\t{voice.Kind}");
        }

        return ExitCodes.Ok;
    }

    private int Convert(CliOptions options)
    {
        if (options.Output is null)
        {
            _error.WriteLine("convert needs -o, since there is nowhere else for the audio to go.");
            return ExitCodes.Usage;
        }

        return Speak(options, FromFile(options));
    }

    /// <summary>The words given on the command line, joined the way the shell split them.</summary>
    private Func<TextReader>? FromArguments(CliOptions options)
    {
        if (options.Arguments.Count == 0)
        {
            _error.WriteLine("say needs something to say.");
            return null;
        }

        var text = string.Join(" ", options.Arguments);
        return () => new StringReader(text);
    }

    private Func<TextReader>? FromFile(CliOptions options)
    {
        if (options.Arguments.Count == 0)
        {
            _error.WriteLine($"{options.Command} needs a file to read.");
            return null;
        }

        var path = options.Arguments[0];
        if (!File.Exists(path))
        {
            _error.WriteLine($"There is no file at {path}.");
            return null;
        }

        if (!PdfTextExtractor.HasPdfExtension(path) && !PdfTextExtractor.LooksLikePdf(path))
        {
            return () => new StreamReader(path);
        }

        // Nothing off Windows recognizes text in a scan, so a page without a text layer is lost.
        var extracted = new PdfTextExtractor().ExtractAsync(path).GetAwaiter().GetResult();
        if (extracted.ScannedPages.Count > 0)
        {
            _error.WriteLine($"{extracted.ScannedPages.Count} page(s) hold no text and were skipped.");
        }

        var text = extracted.Text;
        return () => new StringReader(text);
    }

    private int Speak(CliOptions options, Func<TextReader>? readerFactory)
    {
        if (readerFactory is null) return ExitCodes.Usage;

        if (options.Output is null)
        {
            _error.WriteLine("Playing out loud is not built yet. Give -o <file>, or -o - to pipe the audio.");
            return ExitCodes.Usage;
        }

        var settings = Settings ??= AppSettings.Load();
        var voice = ChooseVoice(options, settings);
        if (voice is null) return ExitCodes.Failed;

        var threads = options.Threads is int given && given > 0 ? given : settings.ResolvedNumThreads;
        using var engine = EngineLoader(voice, threads);

        var chunking = settings.ToChunkerOptions();
        if (options.Filter is SpokenCharacterPolicy policy) chunking.Spoken.Policy = policy;

        var runner = new SynthesisRunner(engine, chunking)
        {
            SpeakerId = options.Speaker ?? settings.GetSpeakerId(voice.Name),
            Speed = options.Speed ?? settings.Speed,
        };

        var total = TextMeasure.CountCharacters(readerFactory);
        using var reader = readerFactory();

        WriteAudio(options, engine, runner, reader, total);

        if (runner.UtterancesSpoken == 0 && total > 0)
        {
            _error.WriteLine("Every character was filtered, so nothing was said. Try --filter off.");
            return ExitCodes.Failed;
        }

        Report(options, runner);
        return ExitCodes.Ok;
    }

    private void WriteAudio(CliOptions options, ITtsEngine engine, SynthesisRunner runner,
        TextReader reader, long total)
    {
        var progress = options.Quiet ? null : new Progress<SynthesisProgress>(ReportProgress);

        if (options.OutputIsStandardOut)
        {
            using var sink = new StreamWaveSink(StandardOutputStream(), engine.SampleRate);
            runner.Run(reader, total, sink, progress);
            return;
        }

        var folder = Path.GetDirectoryName(Path.GetFullPath(options.Output!));
        if (folder is not null) Directory.CreateDirectory(folder);

        using var file = new WaveFileSink(options.Output!, engine.SampleRate);
        runner.Run(reader, total, file, progress);
    }

    private void Report(CliOptions options, SynthesisRunner runner)
    {
        if (options.Quiet) return;

        var filtered = runner.CharactersFiltered > 0
            ? $", {runner.CharactersFiltered} character(s) filtered"
            : string.Empty;

        _error.WriteLine(options.OutputIsStandardOut
            ? $"Done{filtered}."
            : $"Wrote {options.Output}{filtered}.");
    }

    /// <summary>Progress goes to standard error, so standard output stays a clean wave stream.</summary>
    private void ReportProgress(SynthesisProgress progress)
    {
        if (progress.TotalCharacters <= 0) return;
        _error.WriteLine($"{progress.Percent}% ({progress.CharactersRead} of {progress.TotalCharacters})");
    }

    private VoiceDescriptor? ChooseVoice(CliOptions options, AppSettings settings)
    {
        var directory = VoicesDirectory(options);
        var voices = VoiceScanner(directory);

        if (voices.Count == 0)
        {
            _error.WriteLine($"No voices in {directory}. Put one there, or pass --voices.");
            return null;
        }

        var wanted = options.Voice ?? settings.LastVoiceName;
        if (wanted is null) return voices[0];

        var match = voices.FirstOrDefault(v => string.Equals(v.Name, wanted, StringComparison.OrdinalIgnoreCase))
                    ?? voices.FirstOrDefault(v => v.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase));

        if (match is not null) return match;

        // A voice named in settings that is no longer installed is not worth failing a run over.
        if (options.Voice is null) return voices[0];

        _error.WriteLine($"No installed voice matches \"{wanted}\". Run ttsutil voices to see them.");
        return null;
    }

    private string VoicesDirectory(CliOptions options)
    {
        if (options.VoicesDirectory is not null) return options.VoicesDirectory;

        var settings = Settings ??= AppSettings.Load();
        return settings.ResolvedVoicesDirectory;
    }
}
