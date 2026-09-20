/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Runtime.InteropServices;

namespace TtsUtil.Cli.Audio;

/// <summary>Plays 16 bit mono PCM through ALSA, which PulseAudio and PipeWire both answer to,
/// so one path covers every current desktop.</summary>
internal sealed class AlsaPcmDevice : IPcmDevice
{
    private const int StreamPlayback = 0;
    private const int FormatS16Le = 2;
    private const int AccessRwInterleaved = 3;
    private const uint LatencyMicroseconds = 200000;
    private const int TransferBytes = 8192;

    private readonly IntPtr _pcm;
    private readonly byte[] _transfer = GC.AllocateArray<byte>(TransferBytes, pinned: true);
    private bool _pauseUnsupported;
    private bool _disposed;

    private AlsaPcmDevice(IntPtr pcm) => _pcm = pcm;

    public int BytesPerFrame => 2;

    /// <summary>The card to open, overridable for a machine whose default is not the one wanted.</summary>
    public static string DeviceName =>
        Environment.GetEnvironmentVariable("TTSUTIL_ALSA_DEVICE") is { Length: > 0 } name ? name : "default";

    public long FramesPending
    {
        get
        {
            if (_disposed) return 0;
            return snd_pcm_delay(_pcm, out var frames) < 0 ? 0 : Math.Max(0, (long)frames);
        }
    }

    /// <summary>Whether this machine has ALSA at all, asked before a run commits to playing.</summary>
    public static bool IsAvailable =>
        OperatingSystem.IsLinux() && NativeLibraries.CanLoad(NativeLibraries.Alsa);

    /// <summary>Opens the card, or explains why it could not be opened.</summary>
    public static bool TryOpen(int sampleRate, string? deviceName, out AlsaPcmDevice? device, out string error)
    {
        device = null;
        error = string.Empty;

        var name = deviceName is { Length: > 0 } given ? given : DeviceName;

        int opened;
        IntPtr pcm;
        try
        {
            opened = snd_pcm_open(out pcm, name, StreamPlayback, 0);
        }
        catch (DllNotFoundException)
        {
            error = "ALSA is not installed, so there is nothing to play through.";
            return false;
        }

        if (opened < 0)
        {
            error = $"Could not open the {name} sound device: {Describe(opened)}.";
            return false;
        }

        var configured = snd_pcm_set_params(pcm, FormatS16Le, AccessRwInterleaved, 1, (uint)sampleRate, 1,
            LatencyMicroseconds);

        if (configured < 0)
        {
            snd_pcm_close(pcm);
            error = $"The {name} sound device will not take {sampleRate} Hz mono: {Describe(configured)}.";
            return false;
        }

        device = new AlsaPcmDevice(pcm);
        return true;
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        var offset = 0;

        while (offset < data.Length && !_disposed)
        {
            var take = Math.Min(_transfer.Length, data.Length - offset);
            take -= take % BytesPerFrame;
            if (take == 0) return;

            data.Slice(offset, take).CopyTo(_transfer);
            if (!WriteFrames(take / BytesPerFrame)) return;
            offset += take;
        }
    }

    public bool TryPause(bool paused)
    {
        if (_disposed || _pauseUnsupported) return false;

        if (snd_pcm_pause(_pcm, paused ? 1 : 0) >= 0) return true;

        // Plenty of cards cannot hold, and the caller then stops feeding this one instead.
        _pauseUnsupported = true;
        return false;
    }

    public void Drain()
    {
        if (!_disposed) snd_pcm_drain(_pcm);
    }

    public void Drop()
    {
        if (_disposed) return;

        snd_pcm_drop(_pcm);
        snd_pcm_prepare(_pcm);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        snd_pcm_close(_pcm);
    }

    private static string Describe(int error)
    {
        var text = Marshal.PtrToStringUTF8(snd_strerror(error));
        return text is { Length: > 0 } ? text : $"error {error}";
    }

    /// <summary>Hands one buffer to the card, recovering from the underruns a paused or
    /// slow producer causes rather than abandoning the run.</summary>
    private bool WriteFrames(int frames)
    {
        var buffer = Marshal.UnsafeAddrOfPinnedArrayElement(_transfer, 0);
        var remaining = frames;
        var offset = 0;

        while (remaining > 0)
        {
            var written = (long)snd_pcm_writei(_pcm, buffer + (offset * BytesPerFrame), remaining);

            if (written < 0)
            {
                if (snd_pcm_recover(_pcm, (int)written, 1) < 0) return false;
                continue;
            }

            offset += (int)written;
            remaining -= (int)written;
        }

        return true;
    }

    [DllImport(NativeLibraries.Alsa)]
    private static extern int snd_pcm_open(out IntPtr pcm, string name, int stream, int mode);

    [DllImport(NativeLibraries.Alsa)]
    private static extern int snd_pcm_set_params(IntPtr pcm, int format, int access, uint channels,
        uint rate, int softResample, uint latency);

    [DllImport(NativeLibraries.Alsa)]
    private static extern nint snd_pcm_writei(IntPtr pcm, IntPtr buffer, nint frames);

    [DllImport(NativeLibraries.Alsa)]
    private static extern int snd_pcm_recover(IntPtr pcm, int error, int silent);

    [DllImport(NativeLibraries.Alsa)]
    private static extern int snd_pcm_delay(IntPtr pcm, out nint frames);

    [DllImport(NativeLibraries.Alsa)]
    private static extern int snd_pcm_pause(IntPtr pcm, int enable);

    [DllImport(NativeLibraries.Alsa)]
    private static extern int snd_pcm_drain(IntPtr pcm);

    [DllImport(NativeLibraries.Alsa)]
    private static extern int snd_pcm_drop(IntPtr pcm);

    [DllImport(NativeLibraries.Alsa)]
    private static extern int snd_pcm_prepare(IntPtr pcm);

    [DllImport(NativeLibraries.Alsa)]
    private static extern int snd_pcm_close(IntPtr pcm);

    [DllImport(NativeLibraries.Alsa)]
    private static extern IntPtr snd_strerror(int error);
}
