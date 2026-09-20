/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Globalization;
using TtsUtil.Core.Text;

namespace TtsUtil.Cli;

/// <summary>One parsed command line: the verb, its arguments, and the switches.</summary>
internal sealed class CliOptions
{
    /// <summary>The verb, lower cased, or an empty string when none was given.</summary>
    public string Command { get; private set; } = string.Empty;

    /// <summary>Everything after the verb that was not a switch.</summary>
    public List<string> Arguments { get; } = new();

    /// <summary>Where audio goes. A single dash means standard output.</summary>
    public string? Output { get; private set; }

    public string? Voice { get; private set; }

    public int? Speaker { get; private set; }

    public float? Speed { get; private set; }

    public int? Threads { get; private set; }

    public string? VoicesDirectory { get; private set; }

    public SpokenCharacterPolicy? Filter { get; private set; }

    /// <summary>Whether progress is kept off standard error.</summary>
    public bool Quiet { get; private set; }

    public bool Help { get; private set; }

    public bool Version { get; private set; }

    /// <summary>What was wrong with the command line, or null when it parsed.</summary>
    public string? Error { get; private set; }

    /// <summary>True when the output is standard output rather than a file.</summary>
    public bool OutputIsStandardOut => Output == "-";

    public static CliOptions Parse(IReadOnlyList<string> args)
    {
        var options = new CliOptions();

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];

            if (arg.Length == 0) continue;

            if (arg[0] != '-' || arg == "-")
            {
                if (options.Command.Length == 0 && options.Arguments.Count == 0 && arg[0] != '-')
                {
                    options.Command = arg.ToLowerInvariant();
                    continue;
                }

                options.Arguments.Add(arg);
                continue;
            }

            if (!options.ReadSwitch(args, ref i)) return options;
        }

        return options;
    }

    /// <summary>The switches that are followed by something, in both their forms.</summary>
    private static readonly HashSet<string> TakesAValue = new(StringComparer.Ordinal)
    {
        "-o", "--out", "-n", "--voice", "-d", "--voices", "-s", "--speaker",
        "-t", "--threads", "-r", "--speed", "-f", "--filter",
    };

    /// <summary>Reads one switch and whatever value it takes, moving the index past both.</summary>
    private bool ReadSwitch(IReadOnlyList<string> args, ref int i)
    {
        var (name, inlineValue) = Split(args[i]);

        switch (name)
        {
            case "-h":
            case "--help":
                Help = true;
                return true;
            case "--version":
                Version = true;
                return true;
            case "-q":
            case "--quiet":
                Quiet = true;
                return true;
        }

        // Checked before the value is taken, so a trailing unknown option says what is wrong with it.
        if (!TakesAValue.Contains(name)) return Fail($"Unknown option {name}.");

        var value = inlineValue ?? Next(args, ref i);
        if (value is null) return Fail($"{name} needs a value.");

        switch (name)
        {
            case "-o":
            case "--out":
                Output = value;
                return true;
            case "-n":
            case "--voice":
                Voice = value;
                return true;
            case "-d":
            case "--voices":
                VoicesDirectory = value;
                return true;
            case "-s":
            case "--speaker":
                return ReadInt(name, value, speaker => Speaker = speaker);
            case "-t":
            case "--threads":
                return ReadInt(name, value, threads => Threads = threads);
            case "-r":
            case "--speed":
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed))
                {
                    return Fail($"{name} needs a number, not \"{value}\".");
                }

                if (speed < 0.1f || speed > 4.0f) return Fail("--speed has to be between 0.1 and 4.0.");
                Speed = speed;
                return true;
            case "-f":
            case "--filter":
                return ReadFilter(value);
            default:
                return Fail($"Unknown option {name}.");
        }
    }

    private static (string Name, string? Value) Split(string arg)
    {
        var equals = arg.IndexOf('=');
        return equals < 0
            ? (arg.ToLowerInvariant(), null)
            : (arg[..equals].ToLowerInvariant(), arg[(equals + 1)..]);
    }

    private static string? Next(IReadOnlyList<string> args, ref int i) =>
        i + 1 < args.Count ? args[++i] : null;

    private bool ReadInt(string name, string value, Action<int> set)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return Fail($"{name} needs a whole number, not \"{value}\".");
        }

        if (parsed < 0) return Fail($"{name} cannot be negative.");
        set(parsed);
        return true;
    }

    private bool ReadFilter(string value)
    {
        switch (value.ToLowerInvariant())
        {
            case "latin":
                Filter = SpokenCharacterPolicy.LatinOnly;
                return true;
            case "any":
                Filter = SpokenCharacterPolicy.AnyLetter;
                return true;
            case "off":
                Filter = SpokenCharacterPolicy.Off;
                return true;
            default:
                return Fail($"--filter takes latin, any or off, not \"{value}\".");
        }
    }

    private bool Fail(string message)
    {
        Error = message;
        return false;
    }
}
