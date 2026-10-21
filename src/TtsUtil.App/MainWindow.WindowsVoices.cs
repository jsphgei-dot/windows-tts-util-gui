/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Windows;
using TtsUtil.Core.Tts;

namespace TtsUtil.App;

/// <summary>The voices Windows already has, listed beside the downloaded ones.</summary>
public partial class MainWindow
{
    private Func<IReadOnlyList<VoiceDescriptor>> _windowsVoiceScanner = WindowsVoices.Scan;

    /// <summary>Lists the Windows voices. Tests answer without asking Windows, which rescans.</summary>
    internal Func<IReadOnlyList<VoiceDescriptor>> WindowsVoiceScanner
    {
        get => _windowsVoiceScanner;
        set
        {
            _windowsVoiceScanner = value;
            RefreshVoices();
        }
    }

    /// <summary>Loads a voice of either kind. Tests replace it so nothing is synthesized.</summary>
    internal Func<VoiceDescriptor, int, ITtsEngine> EngineLoader { get; set; } = (voice, threads) =>
        voice.Source == VoiceSource.Windows
            ? WindowsTtsEngine.Load(voice) ?? throw new InvalidOperationException(
                $"{voice.Name} is no longer installed in Windows.")
            : VoiceCheck.LoadSherpa(voice, threads);

    private ITtsEngine LoadEngine(VoiceDescriptor voice, int threads) => EngineLoader(voice, threads);

    /// <summary>Names a voice in the picker, saying which are the Windows ones.</summary>
    internal static string VoiceLabel(VoiceDescriptor voice) => voice.Source == VoiceSource.Windows
        ? $"{voice.Name}  (Windows{(voice.Language.Length > 0 ? ", " + voice.Language : string.Empty)})"
        : $"{voice.Name}  ({voice.Kind})";

    private void OnUseWindowsVoicesChanged(object sender, RoutedEventArgs e)
    {
        if (VoiceBox is null) return;

        _settings.UseWindowsVoices = UseWindowsVoicesBox.IsChecked == true;
        _settings.Save();

        // A voice that has just been switched off cannot stay loaded and selected.
        if (!_settings.UseWindowsVoices && _engine?.Voice.Source == VoiceSource.Windows) DisposeEngine();

        RefreshVoices();
    }
}
