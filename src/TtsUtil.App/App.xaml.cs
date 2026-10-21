/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Windows;
using System.Windows.Threading;

namespace TtsUtil.App;

public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += OnUnhandledException;
    }

    /// <summary>Voices directory given as --voices on the command line, if any.</summary>
    internal static string? VoicesDirectoryOverride { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        // A voice check is a child run with no window, which ends as soon as the voice speaks.
        if (e.Args.Length >= 2 && e.Args[0] == VoiceCheck.Argument) Environment.Exit(VoiceCheck.RunChild(e.Args));

        VoicesDirectoryOverride = ReadVoicesArgument(e.Args);
        base.OnStartup(e);
    }

    internal static string? ReadVoicesArgument(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];

            if (arg.StartsWith("--voices=", StringComparison.OrdinalIgnoreCase))
            {
                return Trim(arg["--voices=".Length..]);
            }

            if (string.Equals(arg, "--voices", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
            {
                return Trim(args[i + 1]);
            }
        }

        return null;
    }

    private static string? Trim(string value)
    {
        var trimmed = value.Trim().Trim('"');
        return trimmed.Length == 0 ? null : trimmed;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "TTS Util Win", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
