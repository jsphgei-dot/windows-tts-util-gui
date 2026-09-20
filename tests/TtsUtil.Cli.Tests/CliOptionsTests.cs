using TtsUtil.Core.Audio;
using TtsUtil.Core.Text;
using Xunit;

namespace TtsUtil.Cli.Tests;

public sealed class CliOptionsTests
{
    [Fact]
    public void AnEmptyCommandLineAsksForNothing()
    {
        var options = CliOptions.Parse(Array.Empty<string>());

        Assert.Equal(string.Empty, options.Command);
        Assert.Null(options.Error);
    }

    [Fact]
    public void TheFirstWordIsTheCommandAndTheRestAreItsArguments()
    {
        var options = CliOptions.Parse(new[] { "say", "hello", "there" });

        Assert.Equal("say", options.Command);
        Assert.Equal(new[] { "hello", "there" }, options.Arguments);
    }

    [Fact]
    public void TheCommandIsMatchedWhateverCaseItIsTypedIn()
    {
        Assert.Equal("voices", CliOptions.Parse(new[] { "VOICES" }).Command);
    }

    [Fact]
    public void SwitchesAreTakenOutOfTheArguments()
    {
        var options = CliOptions.Parse(new[] { "say", "hello", "-o", "out.wav", "there" });

        Assert.Equal(new[] { "hello", "there" }, options.Arguments);
        Assert.Equal("out.wav", options.Output);
    }

    [Fact]
    public void AValueCanBeAttachedWithAnEqualsSign()
    {
        var options = CliOptions.Parse(new[] { "say", "--out=out.wav", "--voice=Amy" });

        Assert.Equal("out.wav", options.Output);
        Assert.Equal("Amy", options.Voice);
    }

    [Fact]
    public void ShortAndLongFormsMeanTheSameThing()
    {
        var shortForm = CliOptions.Parse(new[] { "say", "-n", "Amy", "-s", "3", "-r", "1.5", "-t", "4" });
        var longForm = CliOptions.Parse(new[]
        {
            "say", "--voice", "Amy", "--speaker", "3", "--speed", "1.5", "--threads", "4",
        });

        Assert.Equal(longForm.Voice, shortForm.Voice);
        Assert.Equal(longForm.Speaker, shortForm.Speaker);
        Assert.Equal(longForm.Speed, shortForm.Speed);
        Assert.Equal(longForm.Threads, shortForm.Threads);
    }

    [Fact]
    public void ADashOnItsOwnIsAnOutputRatherThanASwitch()
    {
        var options = CliOptions.Parse(new[] { "say", "hello", "-o", "-" });

        Assert.True(options.OutputIsStandardOut);
        Assert.Equal(new[] { "hello" }, options.Arguments);
    }

    [Theory]
    [InlineData("latin", SpokenCharacterPolicy.LatinOnly)]
    [InlineData("any", SpokenCharacterPolicy.AnyLetter)]
    [InlineData("off", SpokenCharacterPolicy.Off)]
    public void TheFilterNamesMapOntoThePolicies(string given, SpokenCharacterPolicy expected)
    {
        Assert.Equal(expected, CliOptions.Parse(new[] { "say", "--filter", given }).Filter);
    }

    [Fact]
    public void AFilterNameNobodyRecognizesIsRefused()
    {
        var options = CliOptions.Parse(new[] { "say", "--filter", "sideways" });

        Assert.Equal("--filter takes latin, any or off, not \"sideways\".", options.Error);
    }

    [Fact]
    public void ASpeedOutsideTheAllowedRangeIsRefused()
    {
        Assert.Equal("--speed has to be between 0.1 and 4.0.",
            CliOptions.Parse(new[] { "say", "--speed", "9" }).Error);
    }

    [Fact]
    public void ASpeedIsReadTheSameWhateverTheMachinesDecimalSeparatorIs()
    {
        Assert.Equal(1.5f, CliOptions.Parse(new[] { "say", "--speed", "1.5" }).Speed);
    }

    [Fact]
    public void ANumberThatIsNotOneIsRefused()
    {
        Assert.Equal("--threads needs a whole number, not \"lots\".",
            CliOptions.Parse(new[] { "say", "--threads", "lots" }).Error);
    }

    [Fact]
    public void ASwitchWithNoValueAfterItIsRefused()
    {
        Assert.Equal("--voice needs a value.", CliOptions.Parse(new[] { "say", "--voice" }).Error);
    }

    [Fact]
    public void AnOptionNobodyRecognizesIsRefused()
    {
        Assert.Equal("Unknown option --sideways.", CliOptions.Parse(new[] { "say", "--sideways" }).Error);
    }

    [Theory]
    [InlineData("wav", AudioOutputFormat.Wav)]
    [InlineData("MP3", AudioOutputFormat.Mp3)]
    public void TheFormatNamesMapOntoTheFileTypes(string given, AudioOutputFormat expected)
    {
        Assert.Equal(expected, CliOptions.Parse(new[] { "say", "--format", given }).Format);
    }

    [Fact]
    public void AFormatNobodyRecognizesIsRefused()
    {
        Assert.Equal("--format takes wav or mp3, not \"flac\".",
            CliOptions.Parse(new[] { "say", "--format", "flac" }).Error);
    }

    [Fact]
    public void ABitRateIsReadInKbpsAndHeldToWhatMp3Allows()
    {
        Assert.Equal(192, CliOptions.Parse(new[] { "say", "--bitrate", "192" }).BitRate);
        Assert.Equal("--bitrate has to be between 8 and 320 kbps.",
            CliOptions.Parse(new[] { "say", "--bitrate", "128000" }).Error);
    }

    [Fact]
    public void TheSoundCardCanBeNamed()
    {
        Assert.Equal("hw:1,0", CliOptions.Parse(new[] { "say", "-a", "hw:1,0" }).Device);
    }

    [Fact]
    public void NothingIsAssumedAboutTheFileTypeUntilItIsAsked()
    {
        var options = CliOptions.Parse(new[] { "say", "hello" });

        Assert.Null(options.Format);
        Assert.Null(options.BitRate);
        Assert.Null(options.Device);
    }

    [Fact]
    public void HelpAndVersionAreNoticedWhereverTheyAppear()
    {
        Assert.True(CliOptions.Parse(new[] { "--help" }).Help);
        Assert.True(CliOptions.Parse(new[] { "say", "hello", "-h" }).Help);
        Assert.True(CliOptions.Parse(new[] { "--version" }).Version);
    }

    [Fact]
    public void QuietIsOffUnlessItIsAskedFor()
    {
        Assert.False(CliOptions.Parse(new[] { "say", "hello" }).Quiet);
        Assert.True(CliOptions.Parse(new[] { "say", "hello", "--quiet" }).Quiet);
    }
}
