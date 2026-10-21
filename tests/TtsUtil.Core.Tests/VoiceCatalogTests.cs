using TtsUtil.Core.Tts;
using Xunit;

namespace TtsUtil.Core.Tests;

public sealed class VoiceCatalogTests : IDisposable
{
    private readonly string _root;

    public VoiceCatalogTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "TtsUtilWinTests", Guid.NewGuid().ToString("N"));
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
            // Leftover temp directories are harmless.
        }
    }

    [Fact]
    public void AMissingDirectoryYieldsNoVoices()
    {
        Assert.Empty(VoiceCatalog.Scan(Path.Combine(_root, "nope")));
    }


    [Fact]
    public void ADirectoryWithoutTokensIsIgnored()
    {
        var dir = NewVoiceDirectory("no-tokens");
        WriteFile(dir, "model.onnx");

        Assert.Null(VoiceCatalog.TryLoad(dir));
    }

    [Fact]
    public void ADirectoryWithoutAModelIsIgnored()
    {
        var dir = NewVoiceDirectory("no-model");
        WriteFile(dir, "tokens.txt");

        Assert.Null(VoiceCatalog.TryLoad(dir));
    }

    [Fact]
    public void APiperLayoutIsDetectedAsVits()
    {
        var dir = NewVoiceDirectory("en_US-example-medium");
        WriteFile(dir, "tokens.txt");
        WriteFile(dir, "en_US-example-medium.onnx", 2048);
        Directory.CreateDirectory(Path.Combine(dir, "espeak-ng-data"));

        var voice = VoiceCatalog.TryLoad(dir);

        Assert.NotNull(voice);
        Assert.Equal(VoiceModelKind.Vits, voice!.Kind);
        Assert.Equal("en_US-example-medium", voice.Name);
        Assert.EndsWith("en_US-example-medium.onnx", voice.ModelPath);
        Assert.NotNull(voice.DataDirPath);
        Assert.Null(voice.VocoderPath);
        Assert.Null(voice.VoicesBinPath);
    }

    [Fact]
    public void AKokoroLayoutIsDetectedByItsVoicesFile()
    {
        var dir = NewVoiceDirectory("kokoro-example");
        WriteFile(dir, "tokens.txt");
        WriteFile(dir, "model.onnx", 4096);
        WriteFile(dir, "voices.bin");

        var voice = VoiceCatalog.TryLoad(dir);

        Assert.NotNull(voice);
        Assert.Equal(VoiceModelKind.Kokoro, voice!.Kind);
        Assert.EndsWith("model.onnx", voice.ModelPath);
        Assert.NotNull(voice.VoicesBinPath);
    }

    [Fact]
    public void AMatchaLayoutIsDetectedByItsVocoder()
    {
        var dir = NewVoiceDirectory("matcha-example");
        WriteFile(dir, "tokens.txt");
        WriteFile(dir, "model-steps-3.onnx", 4096);
        WriteFile(dir, "hifigan_v2.onnx", 2048);

        var voice = VoiceCatalog.TryLoad(dir);

        Assert.NotNull(voice);
        Assert.Equal(VoiceModelKind.Matcha, voice!.Kind);
        Assert.EndsWith("model-steps-3.onnx", voice.ModelPath);
        Assert.EndsWith("hifigan_v2.onnx", voice.VocoderPath);
    }

    [Fact]
    public void OptionalFilesArePickedUpWhenPresent()
    {
        var dir = NewVoiceDirectory("with-extras");
        WriteFile(dir, "tokens.txt");
        WriteFile(dir, "model.onnx", 2048);
        WriteFile(dir, "lexicon.txt");
        WriteFile(dir, "LICENSE");
        WriteFile(dir, "MODEL_CARD");
        Directory.CreateDirectory(Path.Combine(dir, "dict"));

        var voice = VoiceCatalog.TryLoad(dir)!;

        Assert.NotNull(voice.LexiconPath);
        Assert.NotNull(voice.LicensePath);
        Assert.NotNull(voice.ModelCardPath);
        Assert.NotNull(voice.DictDirPath);
    }

    [Fact]
    public void TheLargestModelWinsWhenAVitsFolderHoldsSeveral()
    {
        var dir = NewVoiceDirectory("several-models");
        WriteFile(dir, "tokens.txt");
        WriteFile(dir, "small.onnx", 128);
        WriteFile(dir, "large.onnx", 9000);

        var voice = VoiceCatalog.TryLoad(dir)!;

        Assert.EndsWith("large.onnx", voice.ModelPath);
    }

    /// <summary>The picker follows the order voices arrived in, not the alphabet.</summary>
    [Fact]
    public void ScanReturnsEveryVoiceOldestFolderFirst()
    {
        var stamp = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        foreach (var name in new[] { "zebra", "alpha", "middle" })
        {
            var dir = NewVoiceDirectory(name);
            WriteFile(dir, "tokens.txt");
            WriteFile(dir, "model.onnx", 1024);
            Directory.SetCreationTimeUtc(dir, stamp);
            stamp = stamp.AddMinutes(5);
        }

        NewVoiceDirectory("not-a-voice");

        var voices = VoiceCatalog.Scan(_root);

        Assert.Equal(new[] { "zebra", "alpha", "middle" }, voices.Select(v => v.Name));
    }

    [Fact]
    public void AKittenModelIsToldApartFromKokoroByItsModelType()
    {
        var dir = NewVoiceDirectory("some-voice");
        WriteFile(dir, "tokens.txt");
        WriteFile(dir, "voices.bin");
        File.WriteAllBytes(Path.Combine(dir, "model.fp16.onnx"), OnnxMetadataTests.Model(("model_type", "kitten-tts")));

        Assert.Equal(VoiceModelKind.Kitten, VoiceCatalog.TryLoad(dir)!.Kind);
    }

    [Fact]
    public void AKittenFolderIsKittenWhenTheModelSaysNothing()
    {
        var dir = NewVoiceDirectory("kitten-nano-en-v0_1-fp16");
        WriteFile(dir, "tokens.txt");
        WriteFile(dir, "voices.bin");
        WriteFile(dir, "model.onnx", 4096);

        Assert.Equal(VoiceModelKind.Kitten, VoiceCatalog.TryLoad(dir)!.Kind);
    }

    [Fact]
    public void AKokoroModelStaysKokoroWhenItSaysSo()
    {
        var dir = NewVoiceDirectory("kitten-named-but-kokoro");
        WriteFile(dir, "tokens.txt");
        WriteFile(dir, "voices.bin");
        File.WriteAllBytes(Path.Combine(dir, "model.onnx"), OnnxMetadataTests.Model(("model_type", "kokoro")));

        Assert.Equal(VoiceModelKind.Kokoro, VoiceCatalog.TryLoad(dir)!.Kind);
    }

    private string NewVoiceDirectory(string name)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteFile(string directory, string name, int size = 16) =>
        File.WriteAllBytes(Path.Combine(directory, name), new byte[size]);
}
