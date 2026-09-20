/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TtsUtil.Cli.Audio;

/// <summary>Finds the sound libraries under whichever name the distribution installed them by,
/// since a machine may carry libasound.so.2 without the libasound.so a linker would want.</summary>
internal static class NativeLibraries
{
    public const string Alsa = "asound";

    public const string Lame = "mp3lame";

    private static readonly string[] AlsaNames = { "libasound.so.2", "libasound.so" };

    private static readonly string[] LameNames =
    {
        "libmp3lame.so.0", "libmp3lame.so", "libmp3lame.0.dylib", "libmp3lame.dylib", "libmp3lame.dll",
    };

    [ModuleInitializer]
    internal static void Register() =>
        NativeLibrary.SetDllImportResolver(typeof(NativeLibraries).Assembly, Resolve);

    /// <summary>Whether the library is on this machine, asked before anything tries to use it.</summary>
    public static bool CanLoad(string name)
    {
        if (!TryLoad(name, out var handle)) return false;

        NativeLibrary.Free(handle);
        return true;
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) =>
        TryLoad(libraryName, out var handle) ? handle : IntPtr.Zero;

    private static bool TryLoad(string libraryName, out IntPtr handle)
    {
        handle = IntPtr.Zero;

        foreach (var candidate in CandidatesFor(libraryName))
        {
            if (NativeLibrary.TryLoad(candidate, out handle)) return true;
        }

        return false;
    }

    private static string[] CandidatesFor(string libraryName) => libraryName switch
    {
        Alsa => AlsaNames,
        Lame => LameNames,
        _ => Array.Empty<string>(),
    };
}
