/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Text.Json;

namespace TtsUtil.Core.Tts;

/// <summary>The voices that have loaded and spoken once in a separate process, each tied to
/// the model file as it was then.</summary>
public sealed class VoiceCheckLedger
{
    private readonly object _lock = new();
    private readonly string _path;
    private Dictionary<string, string>? _passed;

    public VoiceCheckLedger(string path) => _path = path;

    /// <summary>True when this voice, with this model file, has passed before.</summary>
    public bool HasPassed(VoiceDescriptor voice)
    {
        lock (_lock)
        {
            return Passed().TryGetValue(Key(voice), out var print) && print == Fingerprint(voice);
        }
    }

    /// <summary>Records a pass and writes the ledger out.</summary>
    public void RecordPass(VoiceDescriptor voice)
    {
        lock (_lock)
        {
            Passed()[Key(voice)] = Fingerprint(voice);

            try
            {
                File.WriteAllText(_path, JsonSerializer.Serialize(_passed));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // An unwritten ledger only means the check runs again next time.
            }
        }
    }

    private Dictionary<string, string> Passed()
    {
        if (_passed is not null) return _passed;

        try
        {
            _passed = File.Exists(_path)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path))
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _passed = null;
        }

        return _passed ??= new Dictionary<string, string>();
    }

    private static string Key(VoiceDescriptor voice) => Path.GetFullPath(voice.Directory).ToLowerInvariant();

    /// <summary>The kind, size and time of the model, so a replaced model is checked again.</summary>
    internal static string Fingerprint(VoiceDescriptor voice)
    {
        var model = new FileInfo(voice.ModelPath);
        var stamp = model.Exists ? $"{model.Length}:{model.LastWriteTimeUtc.Ticks}" : "missing";
        return $"{voice.Kind}:{stamp}";
    }
}
