using System.Text;
using TtsUtil.Core.Settings;
using TtsUtil.Core.Tts;
using Xunit;

namespace TtsUtil.Cli.Tests;

public sealed class CliRunnerTests : IDisposable
{
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly MemoryStream _piped = new();
    private readonly string _root;
    private readonly FakeEngine _engine = new();

    public CliRunnerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "TtsUtilCli", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
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

    [Fact]
    public void VersionSaysWhichBuildThisIs()
    {
        Assert.Equal(ExitCodes.Ok, Run("--version"));
        Assert.StartsWith("ttsutil ", _out.ToString());
    }

    [Fact]
    public void NothingAtAllPrintsTheUsage()
    {
        Assert.Equal(ExitCodes.Ok, Run());
        Assert.Contains("ttsutil say <text>", _out.ToString());
    }

    [Fact]
    public void ACommandNobodyRecognizesIsAUsageError()
    {
        Assert.Equal(ExitCodes.Usage, Run("sing"));
        Assert.Contains("There is no sing command.", _error.ToString());
    }

    [Fact]
    public void ABadOptionIsAUsageErrorRatherThanACrash()
    {
        Assert.Equal(ExitCodes.Usage, Run("say", "hello", "--sideways"));
        Assert.Contains("Unknown option --sideways.", _error.ToString());
    }

    // --- voices ---

    [Fact]
    public void VoicesListsWhatIsInstalledOnePerLine()
    {
        Assert.Equal(ExitCodes.Ok, Run("voices"));

        var lines = _out.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(new[] { "Amy\ten-GB\tVits", "Brian\ten-GB\tVits" }, lines);
    }

    [Fact]
    public void VoicesWithNoneInstalledSaysWhereItLooked()
    {
        var runner = NewRunner();
        runner.VoiceScanner = _ => Array.Empty<VoiceDescriptor>();

        Assert.Equal(ExitCodes.Failed, runner.Run(new[] { "voices", "--voices", _root }));
        Assert.Contains($"No voices in {_root}.", _error.ToString());
    }

    // --- say ---

    [Fact]
    public void SayingSomethingWithNowhereToPutItSaysSo()
    {
        Assert.Equal(ExitCodes.Usage, Run("say", "hello there"));
        Assert.Contains("Playing out loud is not built yet.", _error.ToString());
    }

    [Fact]
    public void SayingNothingAtAllIsAUsageError()
    {
        Assert.Equal(ExitCodes.Usage, Run("say", "-o", Path.Combine(_root, "out.wav")));
        Assert.Contains("say needs something to say.", _error.ToString());
    }

    [Fact]
    public void SayingSomethingWritesAWaveFileAndNamesIt()
    {
        var path = Path.Combine(_root, "out.wav");

        Assert.Equal(ExitCodes.Ok, Run("say", "Hello there.", "-o", path));

        Assert.True(new FileInfo(path).Length > 44);
        Assert.Contains($"Wrote {path}.", _error.ToString());
        Assert.Equal(new[] { "Hello there." }, _engine.Spoken);
    }

    [Fact]
    public void TheWordsAreJoinedTheWayTheShellSplitThem()
    {
        Run("say", "Hello", "there", "-o", Path.Combine(_root, "out.wav"));

        Assert.Equal(new[] { "Hello there" }, _engine.Spoken);
    }

    [Fact]
    public void AnOutputOfADashPipesARiffStreamToStandardOutput()
    {
        Assert.Equal(ExitCodes.Ok, Run("say", "Hello there.", "-o", "-"));

        var bytes = _piped.ToArray();
        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(bytes, 8, 4));
        Assert.True(bytes.Length > 44);

        // Nothing but audio may reach standard output, or the pipe stops being a wave stream.
        Assert.Equal(string.Empty, _out.ToString());
    }

    [Fact]
    public void ProgressGoesToStandardErrorUnlessItIsHushed()
    {
        Run("say", "Hello there.", "-o", Path.Combine(_root, "loud.wav"));
        Assert.Contains("100%", _error.ToString());

        var quiet = new StringWriter();
        var runner = new CliRunner(new StringWriter(), quiet)
        {
            EngineLoader = (_, _) => _engine,
            VoiceScanner = _ => Voices(),
            Settings = NewSettings(),
        };

        runner.Run(new[] { "say", "Hello there.", "-o", Path.Combine(_root, "quiet.wav"), "--quiet" });
        Assert.Equal(string.Empty, quiet.ToString());
    }

    [Fact]
    public void AVoiceCanBeChosenByNameOrByPartOfIt()
    {
        Run("say", "Hello.", "-o", Path.Combine(_root, "a.wav"), "--voice", "brian");
        Assert.Equal("Brian", _engine.LoadedVoice);

        Run("say", "Hello.", "-o", Path.Combine(_root, "b.wav"), "--voice", "Am");
        Assert.Equal("Amy", _engine.LoadedVoice);
    }

    [Fact]
    public void AVoiceNameNothingMatchesStopsTheRun()
    {
        Assert.Equal(ExitCodes.Failed,
            Run("say", "Hello.", "-o", Path.Combine(_root, "a.wav"), "--voice", "Kevin"));

        Assert.Contains("No installed voice matches \"Kevin\".", _error.ToString());
    }

    [Fact]
    public void TheVoiceInSettingsIsUsedWhenNoneIsGivenOnTheLine()
    {
        var runner = NewRunner();
        runner.Settings!.LastVoiceName = "Brian";

        runner.Run(new[] { "say", "Hello.", "-o", Path.Combine(_root, "a.wav") });

        Assert.Equal("Brian", _engine.LoadedVoice);
    }

    [Fact]
    public void ASettingsVoiceThatIsGoneFallsBackRatherThanFailing()
    {
        var runner = NewRunner();
        runner.Settings!.LastVoiceName = "A voice that was removed";

        Assert.Equal(ExitCodes.Ok, runner.Run(new[] { "say", "Hello.", "-o", Path.Combine(_root, "a.wav") }));
        Assert.Equal("Amy", _engine.LoadedVoice);
    }

    [Fact]
    public void SpeedAndSpeakerOnTheLineBeatTheSettings()
    {
        var runner = NewRunner();
        runner.Settings!.Speed = 1.0f;
        runner.Settings.SpeakerId = 0;

        runner.Run(new[]
        {
            "say", "Hello.", "-o", Path.Combine(_root, "a.wav"), "--speed", "1.5", "--speaker", "7",
        });

        Assert.Equal(1.5f, _engine.LastSpeed);
        Assert.Equal(7, _engine.LastSpeaker);
    }

    [Fact]
    public void ThreadsOnTheLineReachTheVoiceLoader()
    {
        Run("say", "Hello.", "-o", Path.Combine(_root, "a.wav"), "--threads", "6");

        Assert.Equal(6, _engine.LoadedThreads);
    }

    [Fact]
    public void TextTheFilterEmptiesIsAFailureRatherThanASilentFile()
    {
        Assert.Equal(ExitCodes.Failed, Run("say", "$$$ ###", "-o", Path.Combine(_root, "a.wav")));
        Assert.Contains("Every character was filtered", _error.ToString());
    }

    [Fact]
    public void TurningTheFilterOffLetsTheSymbolsThrough()
    {
        Assert.Equal(ExitCodes.Ok,
            Run("say", "$$$ ###", "-o", Path.Combine(_root, "a.wav"), "--filter", "off"));

        Assert.NotEmpty(_engine.Spoken);
    }

    // --- read and convert ---

    [Fact]
    public void ReadingAFileSpeaksWhatIsInIt()
    {
        var source = Path.Combine(_root, "chapter.txt");
        File.WriteAllText(source, "The chapter opens.");

        Assert.Equal(ExitCodes.Ok, Run("read", source, "-o", Path.Combine(_root, "a.wav")));
        Assert.Equal(new[] { "The chapter opens." }, _engine.Spoken);
    }

    [Fact]
    public void ReadingAFileThatIsNotThereSaysWhichOne()
    {
        var missing = Path.Combine(_root, "gone.txt");

        Assert.Equal(ExitCodes.Usage, Run("read", missing, "-o", Path.Combine(_root, "a.wav")));
        Assert.Contains($"There is no file at {missing}.", _error.ToString());
    }

    [Fact]
    public void ConvertWithoutAnOutputSaysItNeedsOne()
    {
        var source = Path.Combine(_root, "chapter.txt");
        File.WriteAllText(source, "The chapter opens.");

        Assert.Equal(ExitCodes.Usage, Run("convert", source));
        Assert.Contains("convert needs -o", _error.ToString());
    }

    [Fact]
    public void AFolderThatIsNotThereYetIsMadeForTheOutput()
    {
        var path = Path.Combine(_root, "nested", "deeper", "out.wav");

        Assert.Equal(ExitCodes.Ok, Run("say", "Hello.", "-o", path));
        Assert.True(File.Exists(path));
    }

    // --- Scaffolding ---

    private int Run(params string[] args) => NewRunner().Run(args);

    private CliRunner NewRunner() => new(_out, _error)
    {
        EngineLoader = (voice, threads) =>
        {
            _engine.LoadedVoice = voice.Name;
            _engine.LoadedThreads = threads;
            return _engine;
        },
        VoiceScanner = _ => Voices(),
        Settings = NewSettings(),
        StandardOutputStream = () => _piped,
    };

    private AppSettings NewSettings() => new()
    {
        VoicesDirectory = _root,
        OutputDirectory = _root,
        NumThreads = 2,
    };

    private static IReadOnlyList<VoiceDescriptor> Voices() => new[]
    {
        new VoiceDescriptor
        {
            Name = "Amy", Language = "en-GB", Kind = VoiceModelKind.Vits, Source = VoiceSource.Sherpa,
        },
        new VoiceDescriptor
        {
            Name = "Brian", Language = "en-GB", Kind = VoiceModelKind.Vits, Source = VoiceSource.Sherpa,
        },
    };

    /// <summary>A voice that produces a tenth of a second per utterance and remembers what it was
    /// asked to say.</summary>
    private sealed class FakeEngine : ITtsEngine
    {
        public List<string> Spoken { get; } = new();

        public string? LoadedVoice { get; set; }

        public int LoadedThreads { get; set; }

        public float LastSpeed { get; private set; }

        public int LastSpeaker { get; private set; }

        public VoiceDescriptor Voice { get; } = new() { Name = "Amy", Source = VoiceSource.Sherpa };

        public int SampleRate => 16000;

        public int SpeakerCount => 8;

        public void Synthesize(string text, int speakerId, float speed, SampleCallback onSamples,
            CancellationToken cancellationToken)
        {
            Spoken.Add(text);
            LastSpeaker = speakerId;
            LastSpeed = speed;
            onSamples(new float[SampleRate / 10]);
        }

        public void Dispose()
        {
        }
    }
}
