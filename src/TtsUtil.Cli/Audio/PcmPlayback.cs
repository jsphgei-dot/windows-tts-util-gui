/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using TtsUtil.Core.Audio;
using TtsUtil.Core.Tts;

namespace TtsUtil.Cli.Audio;

/// <summary>Plays a run through a sound card as it is synthesized. Everything about pausing,
/// stopping and where the listener has reached lives here rather than in the interop.</summary>
internal sealed class PcmPlayback : IAudioPlayback
{
    private const int ChunkBytes = 4096;

    private readonly IPcmDevice _device;
    private readonly WaveFormatInfo _format;
    private readonly CancellationToken _cancellationToken;
    private readonly PlaybackMarks _marks = new();
    private readonly object _gate = new();
    private byte[] _scratch = Array.Empty<byte>();
    private long _bytesQueued;
    private bool _deviceIsHolding;
    private bool _disposed;
    private volatile bool _paused;

    public PcmPlayback(IPcmDevice device, int sampleRate, CancellationToken cancellationToken)
    {
        _device = device;
        _format = WaveFormatInfo.Pcm16Mono(sampleRate);
        _cancellationToken = cancellationToken;
    }

    public bool IsPaused => _paused;

    /// <summary>Where the listener has reached, which trails what has been synthesized.</summary>
    public long PlayedCharacters => _marks.CharactersAt(PlayedBytes());

    public void Mark(long characterOffset) => _marks.Add(_bytesQueued, characterOffset);

    public void Pause()
    {
        if (_paused) return;

        _paused = true;
        lock (_gate) _deviceIsHolding = _device.TryPause(true);
    }

    public void Resume()
    {
        if (!_paused) return;

        lock (_gate)
        {
            if (_deviceIsHolding) _device.TryPause(false);
            _deviceIsHolding = false;
        }

        _paused = false;
    }

    public void WriteSamples(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0) return;

        var needed = samples.Length * 2;
        if (_scratch.Length < needed) _scratch = new byte[Math.Max(needed, ChunkBytes)];

        var offset = 0;
        foreach (var sample in samples)
        {
            var scaled = (int)MathF.Round(Math.Clamp(sample, -1f, 1f) * short.MaxValue);
            var value = (short)Math.Clamp(scaled, short.MinValue, short.MaxValue);
            _scratch[offset++] = (byte)(value & 0xff);
            _scratch[offset++] = (byte)((value >> 8) & 0xff);
        }

        Write(_scratch.AsSpan(0, needed));
    }

    public void WriteSilence(int milliseconds)
    {
        if (milliseconds <= 0) return;

        var bytes = _format.BytesForDuration(milliseconds / 1000.0);
        if (bytes <= 0) return;

        var zeros = new byte[Math.Min(bytes, ChunkBytes)];
        for (var written = 0; written < bytes; written += zeros.Length)
        {
            Write(zeros.AsSpan(0, Math.Min(zeros.Length, bytes - written)));
        }
    }

    /// <summary>Blocks until the card has played what it was given, or the run is canceled.</summary>
    public void WaitUntilDrained()
    {
        // A held card is not draining, but the audio is still owed to the listener.
        while (_paused && !_cancellationToken.IsCancellationRequested)
        {
            Thread.Sleep(50);
        }

        if (_cancellationToken.IsCancellationRequested) return;
        _device.Drain();
    }

    public void Stop()
    {
        _paused = false;

        lock (_gate) _deviceIsHolding = false;

        _marks.Clear();
        _device.Drop();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _device.Dispose();
    }

    /// <summary>Bytes the listener has actually heard, allowing for what sits in the card.</summary>
    private long PlayedBytes() =>
        Math.Max(0, _bytesQueued - (_device.FramesPending * _device.BytesPerFrame));

    private void Write(ReadOnlySpan<byte> data)
    {
        var offset = 0;

        while (offset < data.Length)
        {
            WaitWhilePaused();
            if (_cancellationToken.IsCancellationRequested) return;

            var take = Math.Min(ChunkBytes, data.Length - offset);
            _device.Write(data.Slice(offset, take));
            _bytesQueued += take;
            offset += take;
        }
    }

    /// <summary>A card that cannot hold is held by stopping the feed, so it runs dry instead.</summary>
    private void WaitWhilePaused()
    {
        while (_paused && !_deviceIsHolding && !_cancellationToken.IsCancellationRequested)
        {
            Thread.Sleep(20);
        }
    }
}
