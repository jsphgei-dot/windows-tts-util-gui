/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Text;
using TtsUtil.Core.Audio;
using TtsUtil.Core.Tts;

namespace TtsUtil.Cli;

/// <summary>Writes 16 bit PCM to a stream that cannot be seeked, such as a pipe, by declaring the
/// largest size a RIFF header can hold rather than going back to correct it.</summary>
internal sealed class StreamWaveSink : ISampleSink, IDisposable
{
    private const uint UnknownSize = 0xFFFFFFFF;

    private readonly Stream _stream;
    private readonly WaveFormatInfo _format;
    private readonly bool _leaveOpen;
    private byte[] _scratch = Array.Empty<byte>();
    private bool _disposed;

    public StreamWaveSink(Stream stream, int sampleRate, bool leaveOpen = false)
    {
        _stream = stream;
        _format = WaveFormatInfo.Pcm16Mono(sampleRate);
        _leaveOpen = leaveOpen;
        WriteHeader();
    }

    public void WriteSamples(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0) return;

        var needed = samples.Length * 2;
        if (_scratch.Length < needed) _scratch = new byte[Math.Max(needed, 8192)];

        var offset = 0;
        foreach (var sample in samples)
        {
            var scaled = (int)MathF.Round(Math.Clamp(sample, -1f, 1f) * short.MaxValue);
            var value = (short)Math.Clamp(scaled, short.MinValue, short.MaxValue);
            _scratch[offset++] = (byte)(value & 0xff);
            _scratch[offset++] = (byte)((value >> 8) & 0xff);
        }

        _stream.Write(_scratch, 0, needed);
    }

    public void WriteSilence(int milliseconds)
    {
        if (milliseconds <= 0) return;

        var bytes = _format.BytesForDuration(milliseconds / 1000.0);
        if (bytes <= 0) return;

        var zeros = new byte[Math.Min(bytes, 8192)];
        for (var written = 0; written < bytes; written += zeros.Length)
        {
            _stream.Write(zeros, 0, Math.Min(zeros.Length, bytes - written));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _stream.Flush();
        if (!_leaveOpen) _stream.Dispose();
    }

    private void WriteHeader()
    {
        var writer = new BinaryWriter(_stream, Encoding.ASCII, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(UnknownSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(_format.Channels);
        writer.Write(_format.SampleRate);
        writer.Write(_format.ByteRate);
        writer.Write((short)_format.BlockAlign);
        writer.Write(_format.BitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(UnknownSize);
        writer.Flush();
    }
}
