using System.IO;
using TtsUtil.Core.Settings;
using TtsUtil.Core.Tts;
using Xunit;

namespace TtsUtil.App.Tests;

[Collection("wpf")]
public sealed class WriteJobTests : IDisposable
{
    private readonly WpfFixture _wpf;
    private readonly string _root;

    public WriteJobTests(WpfFixture wpf)
    {
        _wpf = wpf;
        _root = Path.Combine(Path.GetTempPath(), "TtsUtilWinWriteJobs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "voices"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Both voices have to be speaking at once for the barrier to let either of them go.</summary>
    [Fact]
    public async Task TwoTabsAreWrittenAtTheSameTime()
    {
        using var together = new Barrier(2);
        var window = CreateWindow(() => new FakeEngine(together));

        var first = Path.Combine(_root, "first.wav");
        var second = Path.Combine(_root, "second.wav");

        var writes = _wpf.Invoke(() =>
        {
            window.InputText.Text = "The first one.";
            var other = window.NewDocument(text: "The second one.");

            return Task.WhenAll(
                window.WriteInBackgroundAsync(window.Documents[0], window.Documents[0].Box.Text, first),
                window.WriteInBackgroundAsync(other, other.Box.Text, second));
        });

        await writes;

        Assert.True(new FileInfo(first).Length > 0);
        Assert.True(new FileInfo(second).Length > 0);
        _wpf.Invoke(() =>
        {
            Assert.Empty(window.WriteJobs);
            window.Close();
        });
    }

    [Fact]
    public async Task StopOnASavingTabCancelsTheSaveAndDeletesTheFile()
    {
        var engine = new HoldingEngine();
        var window = CreateWindow(() => engine);
        var path = Path.Combine(_root, "stopped.wav");

        var write = _wpf.Invoke(() =>
        {
            window.InputText.Text = "Words that will not all be written.";
            return window.WriteInBackgroundAsync(window.Documents[0], window.InputText.Text, path);
        });
        Assert.True(engine.Started.Wait(TimeSpan.FromSeconds(10)));
        Assert.True(File.Exists(path));

        _wpf.Invoke(() => window.StopButton.RaiseEvent(
            new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)));
        await write;

        Assert.False(File.Exists(path));
        _wpf.Invoke(() =>
        {
            Assert.Equal("Stopped writing Text 1 and deleted stopped.wav.", window.StatusText.Text);
            Assert.Empty(window.WriteJobs);
            window.Close();
        });
    }

    private MainWindow CreateWindow(Func<ITtsEngine> engine) => _wpf.Invoke(() =>
    {
        var settings = AppSettings.LoadFrom(Path.Combine(_root, "settings.json"));
        settings.VoicesDirectory = Path.Combine(_root, "voices");
        settings.UseWindowsVoices = true;

        var window = new MainWindow(settings, loadVoiceOnSelection: false)
        {
            WindowsVoiceScanner = () => new[]
            {
                new VoiceDescriptor { Name = "David", Source = VoiceSource.Windows, Id = "david", Language = "en-GB" },
            },
            EngineLoader = (_, _) => engine(),
        };

        window.VoiceBox.SelectedIndex = 0;
        return window;
    });

    private sealed class HoldingEngine : ITtsEngine
    {
        public ManualResetEventSlim Started { get; } = new(false);

        public VoiceDescriptor Voice { get; } = new() { Name = "David", Source = VoiceSource.Windows };

        public int SampleRate => 16000;

        public int SpeakerCount => 1;

        public void Synthesize(string text, int speakerId, float speed, SampleCallback onSamples,
            CancellationToken cancellationToken)
        {
            onSamples(new float[SampleRate / 10]);
            Started.Set();
            cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
            cancellationToken.ThrowIfCancellationRequested();
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeEngine : ITtsEngine
    {
        private readonly Barrier _together;

        public FakeEngine(Barrier together) => _together = together;

        public VoiceDescriptor Voice { get; } = new() { Name = "David", Source = VoiceSource.Windows };

        public int SampleRate => 16000;

        public int SpeakerCount => 1;

        public void Synthesize(string text, int speakerId, float speed, SampleCallback onSamples,
            CancellationToken cancellationToken)
        {
            _together.SignalAndWait(TimeSpan.FromSeconds(10), cancellationToken);
            onSamples(new float[SampleRate / 10]);
        }

        public void Dispose()
        {
        }
    }
}
