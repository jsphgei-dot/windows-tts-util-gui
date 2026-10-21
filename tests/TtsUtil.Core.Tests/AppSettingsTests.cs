using TtsUtil.Core.Settings;
using Xunit;

namespace TtsUtil.Core.Tests;

public sealed class AppSettingsTests : IDisposable
{
    private readonly string _root;
    private readonly string _path;

    public AppSettingsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "TtsUtilWinTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _path = Path.Combine(_root, "settings.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Leftover temp directories are harmless.
        }
    }

    [Fact]
    public void LoadingAMissingFileGivesTheDefaults()
    {
        var settings = AppSettings.LoadFrom(_path);

        Assert.Equal(450, settings.SilenceLineEndingMs);
        Assert.Equal(350, settings.SilenceSentenceMs);
        Assert.Equal(500, settings.SilenceQuestionMs);
        Assert.Equal(350, settings.SilenceExclamationMs);
        Assert.Equal(0.9f, settings.Speed);
        Assert.Null(settings.NumThreads);
        Assert.Equal(2000, settings.MaxChunkLength);
        Assert.True(settings.ScaleSilenceToRate);
        Assert.True(settings.FilterHashes);
        Assert.True(settings.FilterWebLinks);
        Assert.True(settings.FilterMailToLinks);
        Assert.Equal(_path, settings.SourcePath);
    }

    [Fact]
    public void SettingsRoundTripThroughDisk()
    {
        var settings = AppSettings.LoadFrom(_path);
        settings.SilenceLineEndingMs = 111;
        settings.SilenceQuestionMs = 222;
        settings.Speed = 1.25f;
        settings.FilterWebLinks = true;
        settings.LastVoiceName = "some-voice";
        settings.SpeakerId = 7;
        settings.Save();

        var reloaded = AppSettings.LoadFrom(_path);

        Assert.Equal(111, reloaded.SilenceLineEndingMs);
        Assert.Equal(222, reloaded.SilenceQuestionMs);
        Assert.Equal(1.25f, reloaded.Speed);
        Assert.True(reloaded.FilterWebLinks);
        Assert.Equal("some-voice", reloaded.LastVoiceName);
        Assert.Equal(7, reloaded.SpeakerId);
    }

    [Fact]
    public void SaveWritesBackToWhereTheSettingsCameFrom()
    {
        var settings = AppSettings.LoadFrom(_path);
        settings.SilenceSentenceMs = 333;

        settings.Save();

        Assert.True(File.Exists(_path));
        Assert.Contains("333", File.ReadAllText(_path));
    }

    [Fact]
    public void CorruptJsonFallsBackToTheDefaults()
    {
        File.WriteAllText(_path, "{ this is not json");

        var settings = AppSettings.LoadFrom(_path);

        Assert.Equal(450, settings.SilenceLineEndingMs);
        Assert.Equal(_path, settings.SourcePath);
    }

    [Fact]
    public void AnUnwritableTargetDoesNotThrow()
    {
        var settings = AppSettings.LoadFrom(_path);

        var exception = Record.Exception(() => settings.SaveTo(Path.Combine(_root, "missing-dir", "settings.json")));

        Assert.Null(exception);
    }

    [Fact]
    public void AnExplicitVoicesDirectoryWins()
    {
        var settings = AppSettings.LoadFrom(_path);
        settings.VoicesDirectory = _root;

        Assert.Equal(_root, settings.ResolvedVoicesDirectory);
    }

    [Fact]
    public void AnExplicitOutputDirectoryWins()
    {
        var settings = AppSettings.LoadFrom(_path);
        settings.OutputDirectory = _root;

        Assert.Equal(_root, settings.ResolvedOutputDirectory);
    }

    [Fact]
    public void WithNoOutputDirectorySavedAudioGoesToAFolderUnderMusic()
    {
        var settings = AppSettings.LoadFrom(_path);
        settings.OutputDirectory = null;

        var music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

        Assert.Equal(Path.Combine(music, "TTS Util"), settings.ResolvedOutputDirectory);
    }

    [Fact]
    public void SettingsMapOntoChunkerOptions()
    {
        var settings = AppSettings.LoadFrom(_path);
        settings.SilenceLineEndingMs = 10;
        settings.SilenceSentenceMs = 20;
        settings.SilenceQuestionMs = 30;
        settings.SilenceExclamationMs = 40;
        settings.ScaleSilenceToRate = true;
        settings.Speed = 1.5f;
        settings.MaxChunkLength = 512;
        settings.FilterHashes = true;
        settings.FilterMailToLinks = true;
        settings.FilterWebLinks = false;

        var options = settings.ToChunkerOptions();

        Assert.Equal(10, options.Silence.LineEndingMs);
        Assert.Equal(20, options.Silence.SentenceMs);
        Assert.Equal(30, options.Silence.QuestionMs);
        Assert.Equal(40, options.Silence.ExclamationMs);
        Assert.True(options.ScaleSilenceToRate);
        Assert.Equal(1.5f, options.SpeechRate);
        Assert.Equal(512, options.MaxChunkLength);
        Assert.True(options.Filters.FilterHashes);
        Assert.True(options.Filters.FilterMailToLinks);
        Assert.False(options.Filters.FilterWebLinks);
    }

    [Fact]
    public void ResettingPutsEverythingBackButKeepsTheFileItCameFrom()
    {
        var settings = AppSettings.LoadFrom(_path);
        settings.Speed = 1.8f;
        settings.MaxChunkLength = 512;
        settings.PauseWhenUnfocused = true;
        settings.SpeakerIds["piper"] = 4;

        settings.ResetToDefaults();

        Assert.Equal(0.9f, settings.Speed);
        Assert.Equal(2000, settings.MaxChunkLength);
        Assert.False(settings.PauseWhenUnfocused);
        Assert.Empty(settings.SpeakerIds);
        Assert.Equal(_path, settings.SourcePath);
        Assert.Equal(0.9f, AppSettings.LoadFrom(_path).Speed);
    }
}
