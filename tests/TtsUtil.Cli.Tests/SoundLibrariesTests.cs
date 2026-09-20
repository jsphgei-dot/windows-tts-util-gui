using TtsUtil.Cli.Audio;
using Xunit;

namespace TtsUtil.Cli.Tests;

public sealed class SoundLibrariesTests
{
    [Fact]
    public void AnOsReleaseFileGivesUpTheIdTheLikenessAndTheName()
    {
        var system = OsRelease.Parse(new[]
        {
            "NAME=\"Linux Mint\"",
            "ID=linuxmint",
            "ID_LIKE=\"ubuntu debian\"",
            "PRETTY_NAME=\"Linux Mint 22\"",
        });

        Assert.Equal("linuxmint", system.Id);
        Assert.Equal(new[] { "ubuntu", "debian" }, system.Like);
        Assert.Equal("Linux Mint 22", system.Name);
    }

    [Fact]
    public void LinesThatAreNotSettingsArePassedOver()
    {
        var system = OsRelease.Parse(new[] { "# a comment", string.Empty, "ID=fedora" });

        Assert.Equal("fedora", system.Id);
        Assert.Empty(system.Like);
    }

    [Fact]
    public void AnIdIsMatchedInWhateverCaseItWasWritten()
    {
        Assert.Equal("arch", OsRelease.Parse(new[] { "ID=Arch" }).Id);
    }

    [Fact]
    public void ASystemThatSaidNothingUsefulStillHasAName()
    {
        Assert.NotEqual(string.Empty, OsRelease.Parse(Array.Empty<string>()).Name);
    }

    [Theory]
    [InlineData("fedora", "", "sudo dnf install alsa-lib")]
    [InlineData("rocky", "rhel centos fedora", "sudo dnf install alsa-lib")]
    [InlineData("debian", "", "sudo apt install libasound2")]
    [InlineData("ubuntu", "debian", "sudo apt install libasound2t64")]
    [InlineData("linuxmint", "ubuntu", "sudo apt install libasound2t64")]
    [InlineData("arch", "", "sudo pacman -S alsa-lib")]
    [InlineData("omarchy", "arch", "sudo pacman -S alsa-lib")]
    [InlineData("opensuse-tumbleweed", "opensuse suse", "sudo zypper install libasound2")]
    [InlineData("alpine", "", "sudo apk add alsa-lib")]
    public void EachFamilyIsToldToInstallThePackageItActuallyCallsIt(string id, string like, string expected)
    {
        var message = SoundLibraries.PlaybackMissing(Distribution(id, like));

        Assert.Contains(SoundLibraries.AlsaFile, message);
        Assert.Contains(expected, message);
    }

    [Theory]
    [InlineData("fedora", "", "sudo dnf install lame-libs")]
    [InlineData("debian", "", "sudo apt install libmp3lame0")]
    [InlineData("arch", "", "sudo pacman -S lame")]
    [InlineData("gentoo", "", "sudo emerge media-sound/lame")]
    public void TheEncoderIsAskedForByTheNameTheFamilyGivesIt(string id, string like, string expected)
    {
        var message = SoundLibraries.EncoderMissing(Distribution(id, like));

        Assert.Contains(SoundLibraries.LameFile, message);
        Assert.Contains(expected, message);
    }

    [Fact]
    public void ADistributionNobodyHasHeardOfIsToldWhatToLookFor()
    {
        var message = SoundLibraries.PlaybackMissing(Distribution("something-new", string.Empty));

        Assert.Contains(SoundLibraries.AlsaFile, message);
        Assert.Contains("whichever package your distribution ships it in", message);
        Assert.DoesNotContain("sudo", message);
    }

    [Fact]
    public void TheSystemIsNamedBackToTheUserSoTheAdviceCanBeTrusted()
    {
        var system = OsRelease.Parse(new[] { "ID=fedora", "PRETTY_NAME=\"Fedora Linux 42\"" });

        Assert.Contains("Fedora Linux 42", SoundLibraries.PlaybackMissing(system));
    }

    [Fact]
    public void ASystemWithNoAlsaAtAllIsPointedAtAFileInsteadOfAPackage()
    {
        var message = SoundLibraries.PlaybackMissing(OsRelease.Parse(Array.Empty<string>()));

        Assert.Contains(OperatingSystem.IsLinux() ? "Install" : "-o -", message);
    }

    [Fact]
    public void TheReportSaysWhichOfTheTwoLibrariesAreHere()
    {
        var system = OsRelease.Parse(new[] { "ID=fedora", "PRETTY_NAME=\"Fedora Linux 42\"" });
        var lines = SoundLibraries.Report(system, name => name == "asound").ToArray();

        Assert.Contains("Fedora Linux 42", lines[0]);
        Assert.Contains($"{SoundLibraries.AlsaFile} found", lines[1]);
        Assert.Contains($"{SoundLibraries.LameFile} not found", lines[2]);
    }

    [Fact]
    public void TheReportOwnsUpToHavingReadTheSystemFile()
    {
        var lines = SoundLibraries.Report(OsRelease.Parse(new[] { "ID=fedora" }), _ => true).ToArray();

        Assert.Contains(OsRelease.Path, lines[3]);
        Assert.Contains(OsRelease.OptOut, lines[3]);
    }

    [Fact]
    public void TheReportSaysWhenTheReadingWasTurnedOff()
    {
        var lines = SoundLibraries.Report(OsRelease.Declined, _ => true).ToArray();

        Assert.Contains(OsRelease.OptOut, lines[3]);
        Assert.Contains("Nothing was read", lines[3]);
    }

    [Fact]
    public void AdviceTakenFromTheSystemFileSaysWhereItCameFrom()
    {
        var message = SoundLibraries.PlaybackMissing(Distribution("fedora", string.Empty));

        Assert.Contains(OsRelease.Path, message);
        Assert.Contains($"Set {OsRelease.OptOut}=1", message);
    }

    [Fact]
    public void TurningTheReadingOffLeavesTheSystemUnnamedAndTheAdviceGeneral()
    {
        Assert.Equal(SystemOrigin.Declined, OsRelease.Declined.Origin);

        var message = SoundLibraries.EncoderMissing(OsRelease.Declined);

        Assert.DoesNotContain(OsRelease.Path, message);
        Assert.DoesNotContain("sudo", message);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("0", true)]
    [InlineData("false", true)]
    [InlineData("1", false)]
    [InlineData("yes", false)]
    public void TheVariableDecidesWhetherTheSystemFileIsReadAtAll(string? set, bool allowed)
    {
        var before = Environment.GetEnvironmentVariable(OsRelease.OptOut);
        try
        {
            Environment.SetEnvironmentVariable(OsRelease.OptOut, set);
            Assert.Equal(allowed, OsRelease.Allowed);
            if (!allowed) Assert.Equal(SystemOrigin.Declined, OsRelease.Read().Origin);
        }
        finally
        {
            Environment.SetEnvironmentVariable(OsRelease.OptOut, before);
        }
    }

    private static OsRelease Distribution(string id, string like) =>
        OsRelease.Parse(new[] { $"ID={id}", $"ID_LIKE=\"{like}\"" });
}
