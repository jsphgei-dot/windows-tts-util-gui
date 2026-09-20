/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

namespace TtsUtil.Cli.Audio;

/// <summary>A sound card that takes interleaved PCM and plays it. Kept behind an interface so the
/// playback logic can be tested on a machine with no sound card at all.</summary>
internal interface IPcmDevice : IDisposable
{
    /// <summary>Bytes in one frame, which is two for the 16 bit mono the voices produce.</summary>
    int BytesPerFrame { get; }

    /// <summary>Hands over every byte, blocking while the card catches up.</summary>
    void Write(ReadOnlySpan<byte> data);

    /// <summary>Frames handed over that the listener has not heard yet.</summary>
    long FramesPending { get; }

    /// <summary>Asks the card to hold. False when it cannot, so the caller stops feeding it.</summary>
    bool TryPause(bool paused);

    /// <summary>Blocks until everything handed over has been played.</summary>
    void Drain();

    /// <summary>Throws away what has not been played and readies the card for the next run.</summary>
    void Drop();
}
