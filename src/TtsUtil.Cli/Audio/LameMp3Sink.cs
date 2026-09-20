/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Runtime.InteropServices;
using TtsUtil.Core.Audio;

namespace TtsUtil.Cli.Audio;

/// <summary>Encodes MP3 as the run goes, through the LAME library every distribution packages,
/// so a long script never has to be held on disk as PCM first.</summary>
internal sealed class LameMp3Sink : IDisposableSink
{
    private const int Mono = 3;
    private const int SamplesPerBlock = 4096;

    private readonly Stream _stream;
    private readonly WaveFormatInfo _format;
    private readonly bool _leaveOpen;
    private readonly IntPtr _lame;
    private readonly byte[] _encoded;
    private short[] _block = Array.Empty<short>();
    private bool _disposed;

    public LameMp3Sink(Stream stream, int sampleRate, int bitRateBitsPerSecond, bool leaveOpen = false)
    {
        _stream = stream;
        _format = WaveFormatInfo.Pcm16Mono(sampleRate);
        _leaveOpen = leaveOpen;
        _encoded = new byte[(int)(SamplesPerBlock * 1.25) + 7200];

        _lame = lame_init();
        if (_lame == IntPtr.Zero) throw new InvalidOperationException("LAME would not start up.");

        lame_set_in_samplerate(_lame, sampleRate);
        lame_set_num_channels(_lame, 1);
        lame_set_mode(_lame, Mono);
        lame_set_brate(_lame, Math.Clamp(bitRateBitsPerSecond / 1000, 8, 320));
        lame_set_quality(_lame, 2);

        // The tag frame would have to be corrected at the end, which a pipe cannot go back to.
        lame_set_bWriteVbrTag(_lame, 0);

        if (lame_init_params(_lame) < 0)
        {
            lame_close(_lame);
            throw new InvalidOperationException($"LAME will not encode {sampleRate} Hz mono.");
        }
    }

    /// <summary>Whether this machine carries LAME, asked before a run commits to writing MP3.</summary>
    public static bool IsAvailable => NativeLibraries.CanLoad(NativeLibraries.Lame);

    public void WriteSamples(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0) return;

        if (_block.Length < SamplesPerBlock) _block = new short[SamplesPerBlock];

        var offset = 0;
        while (offset < samples.Length)
        {
            var take = Math.Min(SamplesPerBlock, samples.Length - offset);

            for (var i = 0; i < take; i++)
            {
                var scaled = (int)MathF.Round(Math.Clamp(samples[offset + i], -1f, 1f) * short.MaxValue);
                _block[i] = (short)Math.Clamp(scaled, short.MinValue, short.MaxValue);
            }

            Encode(take);
            offset += take;
        }
    }

    public void WriteSilence(int milliseconds)
    {
        if (milliseconds <= 0) return;

        var samples = _format.BytesForDuration(milliseconds / 1000.0) / _format.BlockAlign;
        if (samples <= 0) return;

        if (_block.Length < SamplesPerBlock) _block = new short[SamplesPerBlock];

        for (var written = 0; written < samples; written += SamplesPerBlock)
        {
            var take = Math.Min(SamplesPerBlock, samples - written);
            Array.Clear(_block, 0, take);
            Encode(take);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            var flushed = lame_encode_flush(_lame, _encoded, _encoded.Length);
            if (flushed > 0) _stream.Write(_encoded, 0, flushed);
            _stream.Flush();
        }
        finally
        {
            lame_close(_lame);
            if (!_leaveOpen) _stream.Dispose();
        }
    }

    /// <summary>LAME takes both channels, and a mono encoder reads only the first of them.</summary>
    private void Encode(int samples)
    {
        var written = lame_encode_buffer(_lame, _block, _block, samples, _encoded, _encoded.Length);

        if (written < 0) throw new InvalidOperationException($"LAME stopped encoding with error {written}.");
        if (written > 0) _stream.Write(_encoded, 0, written);
    }

    [DllImport(NativeLibraries.Lame)]
    private static extern IntPtr lame_init();

    [DllImport(NativeLibraries.Lame)]
    private static extern int lame_set_in_samplerate(IntPtr lame, int value);

    [DllImport(NativeLibraries.Lame)]
    private static extern int lame_set_num_channels(IntPtr lame, int value);

    [DllImport(NativeLibraries.Lame)]
    private static extern int lame_set_mode(IntPtr lame, int value);

    [DllImport(NativeLibraries.Lame)]
    private static extern int lame_set_brate(IntPtr lame, int value);

    [DllImport(NativeLibraries.Lame)]
    private static extern int lame_set_quality(IntPtr lame, int value);

    [DllImport(NativeLibraries.Lame)]
    private static extern int lame_set_bWriteVbrTag(IntPtr lame, int value);

    [DllImport(NativeLibraries.Lame)]
    private static extern int lame_init_params(IntPtr lame);

    [DllImport(NativeLibraries.Lame)]
    private static extern int lame_encode_buffer(IntPtr lame, short[] left, short[] right, int samples,
        byte[] mp3Buffer, int mp3BufferSize);

    [DllImport(NativeLibraries.Lame)]
    private static extern int lame_encode_flush(IntPtr lame, byte[] mp3Buffer, int mp3BufferSize);

    [DllImport(NativeLibraries.Lame)]
    private static extern int lame_close(IntPtr lame);
}
