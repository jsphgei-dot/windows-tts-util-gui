using TtsUtil.Core.Tts;
using Xunit;

namespace TtsUtil.Core.Tests;

public sealed class VoiceCheckLedgerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "TtsUtilWinTests", Guid.NewGuid().ToString("N"));
    private readonly string _ledger;
    private readonly VoiceDescriptor _voice;

    public VoiceCheckLedgerTests()
    {
        Directory.CreateDirectory(_root);
        _ledger = Path.Combine(_root, "checked-voices.json");
        var model = Path.Combine(_root, "model.onnx");
        File.WriteAllBytes(model, new byte[64]);
        _voice = new VoiceDescriptor { Name = "voice", Directory = _root, ModelPath = model, Kind = VoiceModelKind.Kitten };
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
    public void AVoiceNeverCheckedHasNotPassed()
    {
        Assert.False(new VoiceCheckLedger(_ledger).HasPassed(_voice));
    }

    [Fact]
    public void APassIsRememberedByTheNextSitting()
    {
        new VoiceCheckLedger(_ledger).RecordPass(_voice);

        Assert.True(new VoiceCheckLedger(_ledger).HasPassed(_voice));
    }

    [Fact]
    public void AReplacedModelIsCheckedAgain()
    {
        new VoiceCheckLedger(_ledger).RecordPass(_voice);
        File.WriteAllBytes(_voice.ModelPath, new byte[128]);

        Assert.False(new VoiceCheckLedger(_ledger).HasPassed(_voice));
    }

    [Fact]
    public void AModelReadAsAnotherKindIsCheckedAgain()
    {
        new VoiceCheckLedger(_ledger).RecordPass(_voice);
        var asKokoro = new VoiceDescriptor
        {
            Name = _voice.Name,
            Directory = _voice.Directory,
            ModelPath = _voice.ModelPath,
            Kind = VoiceModelKind.Kokoro,
        };

        Assert.False(new VoiceCheckLedger(_ledger).HasPassed(asKokoro));
    }

    [Fact]
    public void ADamagedLedgerMeansCheckingAgain()
    {
        File.WriteAllText(_ledger, "{ not json");

        Assert.False(new VoiceCheckLedger(_ledger).HasPassed(_voice));
    }
}
