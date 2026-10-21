using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using TtsUtil.Core.Audio;
using TtsUtil.Core.Settings;
using TtsUtil.Core.Tts;
using Xunit;

namespace TtsUtil.App.Tests;

/// <summary>What the one synthesis path does for a reading, for a file written to audio, and for
/// a batch of scripts, which all go through it.</summary>
[Collection("wpf")]
public sealed class SynthesisSeamTests : IDisposable
{
    private readonly WpfFixture _wpf;
    private readonly string _root;
    private readonly string _scriptsDir;
    private readonly List<MainWindow> _windows = new();

    public SynthesisSeamTests(WpfFixture wpf)
    {
        _wpf = wpf;
        _root = Path.Combine(Path.GetTempPath(), "TtsUtilWinSeam", Guid.NewGuid().ToString("N"));
        _scriptsDir = Path.Combine(_root, "scripts");
        Directory.CreateDirectory(Path.Combine(_root, "voices"));
        Directory.CreateDirectory(_scriptsDir);
    }

    public void Dispose()
    {
        _wpf.Invoke(() =>
        {
            foreach (var window in _windows) window.Close();
        });

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    // --- Reading aloud ---

    [Fact]
    public async Task ReadingSpeaksTheTextAndSaysItFinished()
    {
        var engine = new FakeEngine();
        var playback = new StubPlayback();
        var window = CreateWindow(engine, playback);

        _wpf.Invoke(() => window.InputText.Text = "The first sentence. The second one.");
        Click(window.ReadButton);
        await CurrentRun(window);

        _wpf.Invoke(() =>
        {
            Assert.Equal("Finished reading.", window.StatusText.Text);
            Assert.Equal(0, window.Progress.Value);
        });

        Assert.True(engine.Spoken.Count > 0);
        Assert.True(playback.Drained);
    }

    [Fact]
    public async Task AVoiceThatHasSpokenIsWoundBackBeforeTheNextRun()
    {
        var engine = new FakeEngine { NeedsReset = true };
        var window = CreateWindow(engine, new StubPlayback());

        _wpf.Invoke(() => window.InputText.Text = "Something to say.");
        Click(window.ReadButton);
        await CurrentRun(window);

        Assert.Equal(1, engine.Resets);
    }

    [Fact]
    public async Task ASecondReadWhileTheVoiceIsPreparingStartsNothing()
    {
        var engine = new FakeEngine { NeedsReset = true, HoldReset = true };
        var players = 0;
        var window = CreateWindow(engine, new StubPlayback());
        _wpf.Invoke(() =>
        {
            window.PlayerFactory = (_, _) =>
            {
                Interlocked.Increment(ref players);
                return new StubPlayback();
            };
            window.InputText.Text = "Something to say.";
        });

        Click(window.ReadButton);
        var first = CurrentRun(window);
        Assert.True(engine.ResetStarted.Wait(TimeSpan.FromSeconds(10)));

        Click(window.ReadButton);
        var second = CurrentRun(window);
        engine.ResetGate.Set();
        await first;
        await second;

        Assert.Equal(1, players);
        Assert.Equal(1, engine.Resets);
    }

    [Fact]
    public async Task TextTheFilterEmptiesSaysSoRatherThanClaimingItRead()
    {
        var window = CreateWindow(new FakeEngine(), new StubPlayback());

        _wpf.Invoke(() => window.InputText.Text = "$$$ ###");
        Click(window.ReadButton);
        await CurrentRun(window);

        _wpf.Invoke(() => Assert.Equal(
            "Nothing was left to read: every character was filtered. " +
            "Check \"Characters read aloud\" in Settings.",
            window.StatusText.Text));
    }

    [Fact]
    public async Task WhatTheFilterTookIsCountedInTheFinishedMessage()
    {
        var window = CreateWindow(new FakeEngine(), new StubPlayback());

        _wpf.Invoke(() => window.InputText.Text = "Read this $$$ please.");
        Click(window.ReadButton);
        await CurrentRun(window);

        _wpf.Invoke(() =>
        {
            Assert.StartsWith("Finished reading.", window.StatusText.Text);
            Assert.Contains("character(s) filtered.", window.StatusText.Text);
        });
    }

    [Fact]
    public async Task StoppingARunReportsItAndLeavesTheWindowReadyForTheNext()
    {
        var engine = new FakeEngine { HoldUntilCancelled = true };
        var window = CreateWindow(engine, new StubPlayback());

        _wpf.Invoke(() => window.InputText.Text = "A reading that will be cut short.");
        Click(window.ReadButton);
        Assert.True(engine.Started.Wait(TimeSpan.FromSeconds(10)));

        Click(window.StopButton);
        await CurrentRun(window);

        _wpf.Invoke(() => Assert.Equal("Stopped.", window.StatusText.Text));

        // The busy flag is cleared in the finally, so a second reading is not refused.
        engine.HoldUntilCancelled = false;
        _wpf.Invoke(() => window.InputText.Text = "And another one.");
        Click(window.ReadButton);
        await CurrentRun(window);

        _wpf.Invoke(() => Assert.Equal("Finished reading.", window.StatusText.Text));
    }

    [Fact]
    public async Task AVoiceThatThrowsIsReportedInTheStatusBarAndInADialog()
    {
        var errors = new List<(string Message, string Title)>();
        var engine = new FakeEngine { Failure = "the voice fell over" };
        var window = CreateWindow(engine, new StubPlayback());
        _wpf.Invoke(() => window.ErrorReporter = (message, title) => errors.Add((message, title)));

        _wpf.Invoke(() => window.InputText.Text = "This will not be read.");
        Click(window.ReadButton);
        await CurrentRun(window);

        _wpf.Invoke(() => Assert.Equal("Error: the voice fell over", window.StatusText.Text));
        Assert.Equal(("the voice fell over", "Synthesis failed"), Assert.Single(errors));
    }

    [Fact]
    public async Task ASecondRunStartedWhileOneIsWritingIsTurnedAway()
    {
        var engine = new FakeEngine { HoldUntilCancelled = true };
        var window = CreateWindow(engine, new StubPlayback());

        var source = Path.Combine(_root, "source.txt");
        File.WriteAllText(source, "A file being written to audio.");
        var output = Path.Combine(_root, "source.wav");

        _wpf.Invoke(() =>
        {
            window.FilePathBox.Text = source;
            window.AudioPathPicker = _ => output;
            window.InputText.Text = "A reading that should not start.";
        });

        Click(window.ConvertFileButton);
        var writing = _wpf.Invoke(() => window.PendingWork);
        Assert.True(engine.Started.Wait(TimeSpan.FromSeconds(10)));

        Click(window.ReadButton);
        _wpf.Invoke(() => Assert.Equal("Already working. Press Stop first.", window.StatusText.Text));

        Click(window.StopButton);
        await writing;
    }

    // --- Writing one file ---

    [Fact]
    public async Task WritingAFileToAudioNamesWhatItWroteAndLeavesTheFileClosed()
    {
        var window = CreateWindow(new FakeEngine(), new StubPlayback());

        var source = Path.Combine(_root, "chapter.txt");
        File.WriteAllText(source, "The chapter opens. It carries on. It ends.");
        var output = Path.Combine(_root, "chapter.wav");

        _wpf.Invoke(() =>
        {
            window.FilePathBox.Text = source;
            window.AudioPathPicker = _ => output;
        });

        Click(window.ConvertFileButton);
        await _wpf.Invoke(() => window.PendingWork);

        // A trailing progress repaint can land on the status bar after the run, so the message
        // itself is checked in the history rather than in the label.
        _wpf.Invoke(() => Assert.Contains(window.StatusHistory, m => m.StartsWith("Wrote chapter.wav.")));

        // The sink is disposed in the run's finally, so nothing is still holding the file open.
        Assert.True(new FileInfo(output).Length > 0);
        using (File.Open(output, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
        }
    }

    // --- Writing a batch of scripts ---

    [Fact]
    public async Task ABatchWritesOneFileForEachTickedScript()
    {
        WriteScript("First", "The first script has a sentence.");
        WriteScript("Second", "The second script has one too.");

        var window = CreateWindow(new FakeEngine(), new StubPlayback());
        var folder = Path.Combine(_root, "audio");
        Directory.CreateDirectory(folder);

        _wpf.Invoke(() => window.BatchFolderPicker = () => folder);
        Click(window.TickEveryScriptButton);
        Click(window.ConvertScriptsButton);
        await _wpf.Invoke(() => window.PendingWork);

        Assert.Equal(2, Directory.GetFiles(folder, "*.wav").Length);
        _wpf.Invoke(() => Assert.Contains(window.StatusHistory, m => m.StartsWith("Wrote 2 of 2 script(s) to ")));
    }

    [Fact]
    public async Task StoppingPartWayThroughABatchSaysHowManyWereWritten()
    {
        WriteScript("First", "The first script has a sentence.");
        WriteScript("Second", "The second script has one too.");

        var engine = new FakeEngine();
        var window = CreateWindow(engine, new StubPlayback());
        var folder = Path.Combine(_root, "audio");
        Directory.CreateDirectory(folder);

        // The first script is written, and the second is caught holding the voice and stopped.
        engine.OnUtterance = () =>
        {
            if (engine.Spoken.Count <= 1) return;

            engine.HoldUntilCancelled = true;
            _wpf.Invoke(() => Click(window.StopButton));
        };

        _wpf.Invoke(() => window.BatchFolderPicker = () => folder);
        Click(window.TickEveryScriptButton);
        Click(window.ConvertScriptsButton);
        await _wpf.Invoke(() => window.PendingWork);

        _wpf.Invoke(() => Assert.Equal("Stopped. 1 of 2 script(s) were written.", window.StatusText.Text));
    }

    [Fact]
    public async Task ABatchNeverWritesOverAFileAlreadyInTheFolder()
    {
        WriteScript("First", "The first script has a sentence.");

        var window = CreateWindow(new FakeEngine(), new StubPlayback());
        var folder = Path.Combine(_root, "audio");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "First.wav"), "not audio");

        _wpf.Invoke(() => window.BatchFolderPicker = () => folder);
        Click(window.TickEveryScriptButton);
        Click(window.ConvertScriptsButton);
        await _wpf.Invoke(() => window.PendingWork);

        Assert.True(File.Exists(Path.Combine(folder, "First (2).wav")));
        Assert.Equal("not audio", File.ReadAllText(Path.Combine(folder, "First.wav")));
    }

    /// <summary>Opening a script saved with another voice set the picker from code mid run,
    /// which disposed the engine the run was still speaking with.</summary>
    [Fact]
    public async Task AScriptOpenedInANewTabMidRunLeavesTheVoiceAlone()
    {
        var engine = new FakeEngine { HoldUntilCancelled = true };
        var window = CreateWindow(engine, new StubPlayback());
        WriteScript("Other Voice", "Read by Zira.");
        new ScriptLibrary(_scriptsDir).SaveProperties("Other Voice", new ScriptProperties { VoiceName = "Zira" });

        _wpf.Invoke(() => window.InputText.Text = "A reading in progress.");
        Click(window.ReadButton);
        Assert.True(engine.Started.Wait(TimeSpan.FromSeconds(10)));

        _wpf.Invoke(() =>
        {
            window.RefreshScripts();
            window.OpenScriptInNewTab(window.Scripts.List().Single());

            Assert.Equal(0, window.VoiceBox.SelectedIndex);
            Assert.Equal(0, engine.Disposals);
        });

        Click(window.StopButton);
        await CurrentRun(window);
    }

    // --- Scaffolding ---

    private void WriteScript(string title, string text) =>
        File.WriteAllText(Path.Combine(_scriptsDir, title + ".txt"), text);

    private void Click(ButtonBase button) =>
        _wpf.Invoke(() => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));

    private Task CurrentRun(MainWindow window) => _wpf.Invoke(() => window.CurrentRun);

    private MainWindow CreateWindow(ITtsEngine engine, IAudioPlayback playback)
    {
        var window = _wpf.Invoke(() =>
        {
            var settings = AppSettings.LoadFrom(Path.Combine(_root, "settings.json"));
            settings.VoicesDirectory = Path.Combine(_root, "voices");
            settings.UseWindowsVoices = true;
            settings.OutputDirectory = _root;

            // WAV keeps the assertions about the file about the run rather than about the encoder.
            settings.OutputFormat = AudioOutputFormat.Wav;

            var created = new MainWindow(settings, loadVoiceOnSelection: false)
            {
                Scripts = new ScriptLibrary(_scriptsDir),
                WindowsVoiceScanner = () => new[]
                {
                    new VoiceDescriptor
                    {
                        Name = "David", Source = VoiceSource.Windows, Id = "david", Language = "en-GB",
                    },
                    new VoiceDescriptor
                    {
                        Name = "Zira", Source = VoiceSource.Windows, Id = "zira", Language = "en-US",
                    },
                },
                EngineLoader = (_, _) => engine,
                PlayerFactory = (_, _) => playback,
            };

            created.VoiceBox.SelectedIndex = 0;
            created.RefreshScripts();
            return created;
        });

        _windows.Add(window);
        return window;
    }

    /// <summary>A voice that produces a tenth of a second per utterance, and can be told to
    /// misbehave the way a real one does: throw, or sit there until the run is cancelled.</summary>
    private sealed class FakeEngine : ITtsEngine
    {
        public List<string> Spoken { get; } = new();

        public ManualResetEventSlim Started { get; } = new(false);

        public bool HoldUntilCancelled { get; set; }

        public string? Failure { get; set; }

        public bool NeedsReset { get; set; }

        public int Resets { get; private set; }

        /// <summary>Runs on the worker thread once an utterance has been recorded.</summary>
        public Action? OnUtterance { get; set; }

        public VoiceDescriptor Voice { get; } = new() { Name = "David", Source = VoiceSource.Windows };

        public int SampleRate => 16000;

        public int SpeakerCount => 1;

        public bool NeedsVoiceReset => NeedsReset;

        public bool HoldReset { get; set; }

        public ManualResetEventSlim ResetStarted { get; } = new(false);

        public ManualResetEventSlim ResetGate { get; } = new(false);

        public void ResetVoice()
        {
            Resets++;
            ResetStarted.Set();
            if (HoldReset) ResetGate.Wait(TimeSpan.FromSeconds(10));
        }

        public void Synthesize(string text, int speakerId, float speed, SampleCallback onSamples,
            CancellationToken cancellationToken)
        {
            lock (Spoken) Spoken.Add(text);
            Started.Set();
            OnUtterance?.Invoke();

            if (Failure is not null) throw new InvalidOperationException(Failure);

            if (HoldUntilCancelled)
            {
                cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                cancellationToken.ThrowIfCancellationRequested();
            }

            onSamples(new float[SampleRate / 10]);
        }

        public int Disposals { get; private set; }

        public void Dispose() => Disposals++;
    }

    private sealed class StubPlayback : IAudioPlayback
    {
        public bool IsPaused { get; private set; }

        public long PlayedCharacters => 0;

        public bool Drained { get; private set; }

        public void Pause() => IsPaused = true;

        public void Resume() => IsPaused = false;

        public void Stop()
        {
        }

        public void WaitUntilDrained() => Drained = true;

        public void WriteSamples(ReadOnlySpan<float> samples)
        {
        }

        public void WriteSilence(int milliseconds)
        {
        }

        public void Dispose()
        {
        }
    }
}
