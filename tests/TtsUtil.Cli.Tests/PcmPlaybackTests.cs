using TtsUtil.Cli.Audio;
using Xunit;

namespace TtsUtil.Cli.Tests;

/// <summary>What playing a run does to a sound card, tested against a card that is a list of
/// bytes, since the machine running this has no ALSA on it.</summary>
public sealed class PcmPlaybackTests
{
    private const int SampleRate = 16000;

    [Fact]
    public void SamplesReachTheCardAsLittleEndianSixteenBit()
    {
        var device = new FakeDevice();
        using var playback = new PcmPlayback(device, SampleRate, CancellationToken.None);

        playback.WriteSamples(new[] { 0f, 1f, -1f });

        Assert.Equal(new byte[] { 0x00, 0x00, 0xff, 0x7f, 0x01, 0x80 }, device.Written.ToArray());
    }

    [Fact]
    public void ASampleOutsideTheRangeIsClampedRatherThanWrappedAround()
    {
        var device = new FakeDevice();
        using var playback = new PcmPlayback(device, SampleRate, CancellationToken.None);

        playback.WriteSamples(new[] { 4f, -4f });

        Assert.Equal(new byte[] { 0xff, 0x7f, 0x01, 0x80 }, device.Written.ToArray());
    }

    [Fact]
    public void SilenceIsAsLongAsItWasAskedToBe()
    {
        var device = new FakeDevice();
        using var playback = new PcmPlayback(device, SampleRate, CancellationToken.None);

        playback.WriteSilence(100);

        Assert.Equal(SampleRate / 10 * 2, device.Written.Count);
        Assert.All(device.Written, b => Assert.Equal(0, b));
    }

    [Fact]
    public void NoSilenceIsWrittenForNoMilliseconds()
    {
        var device = new FakeDevice();
        using var playback = new PcmPlayback(device, SampleRate, CancellationToken.None);

        playback.WriteSilence(0);

        Assert.Empty(device.Written);
    }

    [Fact]
    public void WhereTheListenerHasReachedTrailsWhatTheCardStillHolds()
    {
        var device = new FakeDevice();
        using var playback = new PcmPlayback(device, SampleRate, CancellationToken.None);

        playback.Mark(0);
        playback.WriteSamples(new float[100]);
        playback.Mark(500);
        playback.WriteSamples(new float[100]);

        // Everything written is still sitting in the card, so nothing has been heard yet.
        device.FramesPending = 200;
        Assert.Equal(0, playback.PlayedCharacters);

        device.FramesPending = 50;
        Assert.Equal(500, playback.PlayedCharacters);
    }

    [Fact]
    public void ACardThatCanHoldIsAskedToRatherThanBeingStarved()
    {
        var device = new FakeDevice { CanPause = true };
        using var playback = new PcmPlayback(device, SampleRate, CancellationToken.None);

        playback.Pause();
        Assert.True(playback.IsPaused);
        Assert.True(device.Holding);

        // The card is holding, so audio may keep arriving at it.
        playback.WriteSamples(new float[10]);
        Assert.Equal(20, device.Written.Count);

        playback.Resume();
        Assert.False(playback.IsPaused);
        Assert.False(device.Holding);
    }

    [Fact]
    public async Task ACardThatCannotHoldIsStarvedUntilTheRunIsResumed()
    {
        var device = new FakeDevice { CanPause = false };
        using var playback = new PcmPlayback(device, SampleRate, CancellationToken.None);

        playback.Pause();
        var writing = Task.Run(() => playback.WriteSamples(new float[10]));

        Assert.False(await Finished(writing));
        Assert.Empty(device.Written);

        playback.Resume();

        await writing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(20, device.Written.Count);
    }

    [Fact]
    public async Task CancelingTheRunReleasesAWriteThatIsWaitingOnAPause()
    {
        using var cancellation = new CancellationTokenSource();
        var device = new FakeDevice { CanPause = false };
        using var playback = new PcmPlayback(device, SampleRate, cancellation.Token);

        playback.Pause();
        var writing = Task.Run(() => playback.WriteSamples(new float[10]));
        Assert.False(await Finished(writing));

        await cancellation.CancelAsync();

        await writing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(device.Written);
    }

    [Fact]
    public void WaitingForTheEndDrainsTheCard()
    {
        var device = new FakeDevice();
        using var playback = new PcmPlayback(device, SampleRate, CancellationToken.None);

        playback.WaitUntilDrained();

        Assert.Equal(1, device.Drains);
    }

    [Fact]
    public void ACanceledRunIsNotWaitedOutToTheEnd()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var device = new FakeDevice();
        using var playback = new PcmPlayback(device, SampleRate, cancellation.Token);

        playback.WaitUntilDrained();

        Assert.Equal(0, device.Drains);
    }

    [Fact]
    public void StoppingThrowsAwayWhatHasNotBeenHeardAndForgetsWhereItWas()
    {
        var device = new FakeDevice();
        using var playback = new PcmPlayback(device, SampleRate, CancellationToken.None);

        playback.Mark(500);
        playback.WriteSamples(new float[100]);
        playback.Pause();
        playback.Stop();

        Assert.Equal(1, device.Drops);
        Assert.False(playback.IsPaused);
        Assert.Equal(0, playback.PlayedCharacters);
    }

    [Fact]
    public void ClosingTheRunStopsTheCardAndLetsGoOfIt()
    {
        var device = new FakeDevice();
        var playback = new PcmPlayback(device, SampleRate, CancellationToken.None);

        playback.Dispose();
        playback.Dispose();

        Assert.Equal(1, device.Drops);
        Assert.Equal(1, device.Disposals);
    }

    /// <summary>Whether the write got through in the time a held card should have stopped it.</summary>
    private static async Task<bool> Finished(Task writing) =>
        ReferenceEquals(writing, await Task.WhenAny(writing, Task.Delay(TimeSpan.FromMilliseconds(200))));

    /// <summary>A sound card that keeps everything it is given, and holds only if told it can.</summary>
    private sealed class FakeDevice : IPcmDevice
    {
        public List<byte> Written { get; } = new();

        public bool CanPause { get; init; }

        public bool Holding { get; private set; }

        public long FramesPending { get; set; }

        public int Drains { get; private set; }

        public int Drops { get; private set; }

        public int Disposals { get; private set; }

        public int BytesPerFrame => 2;

        public void Write(ReadOnlySpan<byte> data) => Written.AddRange(data.ToArray());

        public bool TryPause(bool paused)
        {
            if (!CanPause) return false;

            Holding = paused;
            return true;
        }

        public void Drain() => Drains++;

        public void Drop() => Drops++;

        public void Dispose() => Disposals++;
    }
}
