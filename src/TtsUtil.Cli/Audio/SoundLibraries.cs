/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

namespace TtsUtil.Cli.Audio;

/// <summary>The shared libraries the audio needs, and how each system asks for them.</summary>
internal static class SoundLibraries
{
    /// <summary>One distribution family: what it calls its installer and its packages.</summary>
    private sealed record Family(string Id, string Install, string Alsa, string Lame);

    /// <summary>Matched on the ID of the system first, and only then on what it says it is like.</summary>
    private static readonly Family[] Families =
    {
        new("debian", "sudo apt install", "libasound2", "libmp3lame0"),
        new("ubuntu", "sudo apt install", "libasound2t64 (libasound2 before 24.04)", "libmp3lame0"),
        new("fedora", "sudo dnf install", "alsa-lib", "lame-libs"),
        new("rhel", "sudo dnf install", "alsa-lib", "lame-libs"),
        new("arch", "sudo pacman -S", "alsa-lib", "lame"),
        new("opensuse", "sudo zypper install", "libasound2", "libmp3lame0"),
        new("suse", "sudo zypper install", "libasound2", "libmp3lame0"),
        new("alpine", "sudo apk add", "alsa-lib", "lame-libs"),
        new("gentoo", "sudo emerge", "media-libs/alsa-lib", "media-sound/lame"),
        new("void", "sudo xbps-install", "alsa-lib", "lame"),
        new("nixos", "nix profile install", "nixpkgs#alsa-lib", "nixpkgs#lame"),
    };

    /// <summary>The playback library, under the name the loader looks for.</summary>
    public const string AlsaFile = "libasound.so.2";

    /// <summary>The encoder library, under the name the loader looks for.</summary>
    public const string LameFile = "libmp3lame.so.0";

    /// <summary>Why playback is not available here, and what would make it so.</summary>
    public static string PlaybackMissing(OsRelease system) => SpeaksAlsa(system)
        ? $"Playing out loud needs {AlsaFile}, which {Describe(system)} does not have. "
          + $"{Remedy(system, family => family.Alsa)} "
          + "Failing that, pipe the audio with -o - into a player."
        : $"Playing out loud goes through ALSA, which {Describe(system)} does not carry, "
          + "so here you can write a file or pipe the audio with -o - into a player.";

    /// <summary>Why an MP3 cannot be encoded here, and what would make it possible.</summary>
    public static string EncoderMissing(OsRelease system) => SpeaksAlsa(system)
        ? $"Writing an MP3 needs {LameFile}, which {Describe(system)} does not have. "
          + $"{Remedy(system, family => family.Lame)} "
          + "Failing that, write a WAV with --format wav."
        : $"Writing an MP3 needs the LAME encoder, which {Describe(system)} does not have, "
          + "so here you can write a WAV with --format wav.";

    /// <summary>Whether this system can be played through and can encode, as a line each.</summary>
    public static IEnumerable<string> Report(OsRelease system, Func<string, bool> canLoad)
    {
        yield return $"System    {(system.Origin == SystemOrigin.Declined ? "not looked up" : Describe(system))}";
        yield return $"Playback  {State(AlsaFile, canLoad(NativeLibraries.Alsa))}";
        yield return $"MP3       {State(LameFile, canLoad(NativeLibraries.Lame))}";
        yield return $"Reading   {Disclosure(system)}";
    }

    /// <summary>Says whether the system file was read, and how to stop it being read.</summary>
    private static string Disclosure(OsRelease system) => system.Origin switch
    {
        SystemOrigin.File => $"{OsRelease.Path} was read to name this system. "
                             + $"Set {OsRelease.OptOut}=1 to leave it unread.",
        SystemOrigin.Declined => $"off, since {OsRelease.OptOut} is set. Nothing was read about this system.",
        _ => $"nothing read, since there is no {OsRelease.Path} here.",
    };

    /// <summary>A system that named itself in os-release is a Linux, whatever this build runs on.</summary>
    private static bool SpeaksAlsa(OsRelease system) => system.Id.Length > 0 || OperatingSystem.IsLinux();

    private static string State(string file, bool found) => found ? $"{file} found" : $"{file} not found";

    /// <summary>The install line for this system, or a plain ask when it is one we do not know.</summary>
    private static string Remedy(OsRelease system, Func<Family, string> package)
    {
        var family = Match(system);
        return family is null
            ? "Install whichever package your distribution ships it in."
            : $"Install it with: {family.Install} {package(family)}. "
              + $"(That advice comes from {OsRelease.Path}. Set {OsRelease.OptOut}=1 to leave it unread.)";
    }

    /// <summary>The family named outright, or the nearest one the system claims to resemble.</summary>
    private static Family? Match(OsRelease system) =>
        Array.Find(Families, family => string.Equals(family.Id, system.Id, StringComparison.Ordinal))
        ?? Array.Find(Families, family => system.Resembles(family.Id));

    /// <summary>What to call this system in a sentence.</summary>
    private static string Describe(OsRelease system) =>
        system.Name.Length > 0 ? system.Name : "this system";
}
