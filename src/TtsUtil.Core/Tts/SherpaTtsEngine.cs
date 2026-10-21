/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Runtime.InteropServices;
using SherpaOnnx;

namespace TtsUtil.Core.Tts;

/// <summary>An offline sherpa voice: vits, matcha, kokoro or kitten.</summary>
public sealed class SherpaTtsEngine : ITtsEngine
{
    private readonly object _lock = new();
    private readonly OfflineTtsConfig _config;
    private OfflineTts? _tts;
    private bool _spoken;

    private SherpaTtsEngine(VoiceDescriptor voice, OfflineTtsConfig config, OfflineTts tts)
    {
        Voice = voice;
        _config = config;
        _tts = tts;
        SampleRate = tts.SampleRate;
        SpeakerCount = Math.Max(1, tts.NumSpeakers);
    }

    public VoiceDescriptor Voice { get; }

    public int SampleRate { get; }

    public int SpeakerCount { get; }

    public static SherpaTtsEngine Load(VoiceDescriptor voice, int numThreads = 2, bool debug = false)
    {
        var model = new OfflineTtsModelConfig
        {
            NumThreads = Math.Max(1, numThreads),
            Debug = debug ? 1 : 0,
            Provider = "cpu",
        };

        switch (voice.Kind)
        {
            case VoiceModelKind.Kokoro:
                model.Kokoro.Model = voice.ModelPath;
                model.Kokoro.Voices = voice.VoicesBinPath ?? string.Empty;
                model.Kokoro.Tokens = voice.TokensPath;
                model.Kokoro.DataDir = voice.DataDirPath ?? string.Empty;
                model.Kokoro.DictDir = voice.DictDirPath ?? string.Empty;
                model.Kokoro.Lexicon = voice.LexiconPath ?? string.Empty;
                break;

            case VoiceModelKind.Kitten:
                model.Kitten.Model = voice.ModelPath;
                model.Kitten.Voices = voice.VoicesBinPath ?? string.Empty;
                model.Kitten.Tokens = voice.TokensPath;
                model.Kitten.DataDir = voice.DataDirPath ?? string.Empty;
                break;

            case VoiceModelKind.Matcha:
                model.Matcha.AcousticModel = voice.ModelPath;
                model.Matcha.Vocoder = voice.VocoderPath ?? string.Empty;
                model.Matcha.Tokens = voice.TokensPath;
                model.Matcha.DataDir = voice.DataDirPath ?? string.Empty;
                model.Matcha.DictDir = voice.DictDirPath ?? string.Empty;
                model.Matcha.Lexicon = voice.LexiconPath ?? string.Empty;
                break;

            default:
                model.Vits.Model = voice.ModelPath;
                model.Vits.Tokens = voice.TokensPath;
                model.Vits.DataDir = voice.DataDirPath ?? string.Empty;
                model.Vits.DictDir = voice.DictDirPath ?? string.Empty;
                model.Vits.Lexicon = voice.LexiconPath ?? string.Empty;
                break;
        }

        var config = new OfflineTtsConfig { Model = model, MaxNumSentences = 1 };
        var tts = new OfflineTts(config);

        if (tts.SampleRate <= 0)
        {
            tts.Dispose();
            throw new InvalidOperationException($"Failed to load the voice in \"{voice.Directory}\".");
        }

        return new SherpaTtsEngine(voice, config, tts);
    }

    public void Synthesize(string text, int speakerId, float speed, SampleCallback onSamples, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        lock (_lock)
        {
            var tts = _tts ?? throw new ObjectDisposedException(nameof(SherpaTtsEngine));
            var sid = Math.Clamp(speakerId, 0, Math.Max(0, SpeakerCount - 1));

            OfflineTtsCallback callback = (samplePtr, count) =>
            {
                if (cancellationToken.IsCancellationRequested || count <= 0) return 0;

                var buffer = new float[count];
                Marshal.Copy(samplePtr, buffer, 0, count);
                return onSamples(buffer) ? 1 : 0;
            };

            var audio = tts.GenerateWithCallback(text, speed, sid, callback);
            GC.KeepAlive(callback);
            audio.Dispose();
            _spoken = true;
        }
    }

    /// <summary>
    /// Sherpa draws the prosody noise from one generator that runs on across utterances, so the
    /// second reading of a passage never matches the first. Only a reload winds that generator back.
    /// </summary>
    public bool NeedsVoiceReset
    {
        get
        {
            lock (_lock) return _spoken;
        }
    }

    public void ResetVoice()
    {
        lock (_lock)
        {
            if (!_spoken || _tts is null) return;

            var replacement = new OfflineTts(_config);
            if (replacement.SampleRate <= 0)
            {
                // Keep reading with the voice in hand rather than failing over a cosmetic reset.
                replacement.Dispose();
                return;
            }

            _tts.Dispose();
            _tts = replacement;
            _spoken = false;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _tts?.Dispose();
            _tts = null;
        }
    }
}
