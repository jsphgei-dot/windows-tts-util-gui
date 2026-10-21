/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using TtsUtil.Core.Settings;

namespace TtsUtil.App;

/// <summary>The voice, speaker and speed kept with a script, so it reads the same next time.</summary>
public partial class MainWindow
{
    /// <summary>The window as it is set up now, written down as a script's properties.</summary>
    private ScriptProperties CurrentScriptProperties() => new()
    {
        VoiceName = SelectedVoice?.Name,
        SpeakerId = _speakerId,
        Speed = (float)SpeedSlider.Value,
    };

    /// <summary>Keeps the voice and speed beside a script that has just been saved.</summary>
    private void KeepVoiceWithScript(string title) => Scripts.SaveProperties(title, CurrentScriptProperties());

    /// <summary>Sets the voice, speaker and speed back to the ones a script was saved with. A
    /// voice that is no longer installed leaves the picker alone.</summary>
    internal bool ApplyScriptVoice(string title)
    {
        // The picker is held still during a run, and setting it from here would pull the voice away.
        if (_busy) return false;

        var properties = Scripts.LoadProperties(title);
        if (properties is null) return false;

        SpeedSlider.Value = Math.Clamp(properties.Speed, 0.5, 2.0);

        var index = IndexOfVoice(properties.VoiceName);
        if (index < 0) return false;

        _settings.SetSpeakerId(properties.VoiceName, properties.SpeakerId);
        _speakerId = properties.SpeakerId;
        if (VoiceBox.SelectedIndex != index) VoiceBox.SelectedIndex = index;

        return true;
    }

    private int IndexOfVoice(string? name)
    {
        if (string.IsNullOrEmpty(name)) return -1;

        for (var index = 0; index < _shownVoices.Count; index++)
        {
            if (string.Equals(_shownVoices[index].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }
}
