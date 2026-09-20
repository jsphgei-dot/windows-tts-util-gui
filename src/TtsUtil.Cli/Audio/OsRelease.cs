/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Runtime.InteropServices;

namespace TtsUtil.Cli.Audio;

/// <summary>Where the description of the system came from, which the program says out loud.</summary>
internal enum SystemOrigin
{
    /// <summary>Nothing was read, because there was nothing of the kind to read.</summary>
    None,

    /// <summary>The os-release file was read.</summary>
    File,

    /// <summary>The file was left alone, because the user asked for it to be.</summary>
    Declined,
}

/// <summary>What the machine says it is, read from the os-release file every Linux carries.</summary>
internal sealed record OsRelease(string Id, IReadOnlyList<string> Like, string Name, SystemOrigin Origin)
{
    public const string Path = "/etc/os-release";

    /// <summary>The variable that turns the reading off, for anyone who would rather it did not happen.</summary>
    public const string OptOut = "TTSUTIL_NO_SYSTEM_DETECT";

    /// <summary>A system that did not say, which is every system that carries no os-release.</summary>
    public static OsRelease Unknown { get; } =
        new(string.Empty, Array.Empty<string>(), RuntimeInformation.OSDescription.Trim(), SystemOrigin.None);

    /// <summary>A system that was not asked, so nothing at all is known about it.</summary>
    public static OsRelease Declined { get; } =
        new(string.Empty, Array.Empty<string>(), string.Empty, SystemOrigin.Declined);

    /// <summary>Whether the file may be read, which it may unless the variable says otherwise.</summary>
    public static bool Allowed
    {
        get
        {
            var set = Environment.GetEnvironmentVariable(OptOut);
            return string.IsNullOrEmpty(set) || set == "0" || set.Equals("false", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static OsRelease Read()
    {
        if (!Allowed) return Declined;
        return OperatingSystem.IsLinux() && File.Exists(Path) ? Parse(File.ReadLines(Path)) : Unknown;
    }

    /// <summary>Reads the key and value lines, whose values may or may not be quoted.</summary>
    public static OsRelease Parse(IEnumerable<string> lines)
    {
        var id = string.Empty;
        var like = Array.Empty<string>();
        var name = string.Empty;

        foreach (var line in lines)
        {
            var separator = line.IndexOf('=');
            if (separator <= 0) continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"', '\'');

            switch (key)
            {
                case "ID":
                    id = value.ToLowerInvariant();
                    break;
                case "ID_LIKE":
                    like = value.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    break;
                case "PRETTY_NAME":
                    name = value;
                    break;
            }
        }

        return new OsRelease(id, like, name.Length > 0 ? name : Unknown.Name, SystemOrigin.File);
    }

    /// <summary>Whether this is that distribution, or one built on it.</summary>
    public bool Resembles(string id) =>
        string.Equals(Id, id, StringComparison.Ordinal) || Like.Contains(id, StringComparer.Ordinal);
}
