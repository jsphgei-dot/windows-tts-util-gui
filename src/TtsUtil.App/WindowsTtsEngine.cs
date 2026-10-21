/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.IO;
using TtsUtil.Core.Audio;
using TtsUtil.Core.Tts;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;

namespace TtsUtil.App;

/// <summary>Speaks with a voice Windows already has, so the program works before anything is
/// downloaded.</summary>
public sealed class WindowsTtsEngine : ITtsEngine
{
    /// <summary>What SpeakingRate accepts, outside which it throws.</summary>
    private const double SlowestRate = 0.5;
    private const double FastestRate = 6.0;

    private readonly SpeechSynthesizer _synthesizer;
    private readonly object _lock = new();

    private WindowsTtsEngine(SpeechSynthesizer synthesizer, VoiceDescriptor voice, int sampleRate)
    {
        _synthesizer = synthesizer;
        Voice = voice;
        SampleRate = sampleRate;
    }

    public VoiceDescriptor Voice { get; }

    public int SampleRate { get; private set; }

    /// <summary>Always one. Each Windows voice is its own entry in the list.</summary>
    public int SpeakerCount => 1;

    /// <summary>Loads a Windows voice, or returns null when it is no longer installed.</summary>
    public static WindowsTtsEngine? Load(VoiceDescriptor voice)
    {
        var information = WindowsVoices.Find(voice.Id);
        if (information is null) return null;

        SpeechSynthesizer? synthesizer = null;

        try
        {
            synthesizer = new SpeechSynthesizer { Voice = information };

            // The rate a voice reports nothing about, so one short utterance settles it.
            var probe = Synthesize(synthesizer, "a");
            return new WindowsTtsEngine(synthesizer, voice, probe?.SampleRate ?? 16000);
        }
        catch (Exception)
        {
            synthesizer?.Dispose();
            return null;
        }
    }

    public void Synthesize(string text, int speakerId, float speed, SampleCallback onSamples,
        CancellationToken cancellationToken)
    {
        if (text.Length == 0 || cancellationToken.IsCancellationRequested) return;

        Sound? sound;

        lock (_lock)
        {
            SetRate(_synthesizer, speed);
            sound = Synthesize(_synthesizer, text);
        }

        if (sound is null || cancellationToken.IsCancellationRequested) return;

        SampleRate = sound.SampleRate;
        onSamples(sound.Samples);
    }

    public void Dispose()
    {
        lock (_lock) _synthesizer.Dispose();
    }

    /// <summary>Windows before 1709 has no rate control on these voices, which read at their
    /// own speed there rather than throwing.</summary>
    private static void SetRate(SpeechSynthesizer synthesizer, float speed)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 16299)) return;

        synthesizer.Options.SpeakingRate = Math.Clamp(speed, SlowestRate, FastestRate);
    }

    /// <summary>Runs one utterance and turns the wave it hands back into samples.</summary>
    private static Sound? Synthesize(SpeechSynthesizer synthesizer, string text)
    {
        using var stream = synthesizer.SynthesizeTextToStreamAsync(text).AsTask().GetAwaiter().GetResult();

        var bytes = ReadAll(stream);
        if (bytes.Length == 0) return null;

        using var wave = new MemoryStream(bytes);
        var header = WaveHeader.Read(wave);

        if (header.Format.BitsPerSample != 16) return null;

        wave.Position = header.DataOffset;
        return new Sound(header.Format.SampleRate, ToSamples(wave, header));
    }

    private static byte[] ReadAll(IRandomAccessStream stream)
    {
        var size = (uint)stream.Size;
        if (size == 0) return Array.Empty<byte>();

        using var reader = new DataReader(stream.GetInputStreamAt(0));
        reader.LoadAsync(size).AsTask().GetAwaiter().GetResult();

        var bytes = new byte[size];
        reader.ReadBytes(bytes);

        return bytes;
    }

    /// <summary>Turns interleaved 16 bit samples into the single channel of floats the sinks take.</summary>
    private static float[] ToSamples(Stream wave, WaveHeader header)
    {
        var channels = Math.Max((short)1, header.Format.Channels);
        var frames = header.DataSize / (2 * channels);
        var samples = new float[frames];
        var block = new byte[2 * channels];

        for (var frame = 0; frame < frames; frame++)
        {
            if (wave.Read(block, 0, block.Length) != block.Length) return samples[..frame];

            samples[frame] = BitConverter.ToInt16(block, 0) / 32768f;
        }

        return samples;
    }

    private sealed record Sound(int SampleRate, float[] Samples);
}
