/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Text.Json;
using System.Text.Json.Serialization;
using TtsUtil.Core.Audio;
using TtsUtil.Core.Text;

namespace TtsUtil.Core.Settings;

/// <summary>What happens when a reading reaches its end.</summary>
public enum RepeatMode
{
    /// <summary>Stop at the end, which is how the program has always behaved.</summary>
    Off,

    /// <summary>Start the same script again.</summary>
    One,

    /// <summary>Move to the next in the queue, and wrap round after the last.</summary>
    All,
}

/// <summary>What the program does when a file has finished being written.</summary>
public enum WriteFinishedNotice
{
    /// <summary>Nothing beyond the line in the status bar.</summary>
    None,

    /// <summary>A short sound.</summary>
    Sound,

    /// <summary>A Windows notification as well as the sound.</summary>
    PopupAndSound,
}

/// <summary>User settings, stored beside the executable when that is writable.</summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public string? VoicesDirectory { get; set; }

    public string? LastVoiceName { get; set; }

    /// <summary>The speaker used before choices became per voice, still the fallback.</summary>
    public int SpeakerId { get; set; }

    /// <summary>The chosen speaker for each voice, keyed by lower case voice name.</summary>
    public Dictionary<string, int> SpeakerIds { get; set; } = new();

    /// <summary>Speakers the user starred, keyed by lower case voice name.</summary>
    public Dictionary<string, List<int>> FavouriteSpeakers { get; set; } = new();

    public float Speed { get; set; } = 0.9f;

    /// <summary>Threads the voice model uses, or null to work it out from the processor count.</summary>
    public int? NumThreads { get; set; }

    /// <summary>The thread count actually used, with an unset setting resolved for this machine.</summary>
    [JsonIgnore]
    public int ResolvedNumThreads => ThreadPlan.Resolve(NumThreads);

    public int SilenceLineEndingMs { get; set; } = 450;

    public int SilenceSentenceMs { get; set; } = 350;

    public int SilenceQuestionMs { get; set; } = 500;

    public int SilenceExclamationMs { get; set; } = 350;

    public bool ScaleSilenceToRate { get; set; } = true;

    public bool FilterHashes { get; set; } = true;

    public bool FilterWebLinks { get; set; } = true;

    public bool FilterMailToLinks { get; set; } = true;

    /// <summary>Which characters are allowed to be voiced. Punctuation still becomes silence.</summary>
    public SpokenCharacterPolicy SpokenCharacters { get; set; } = SpokenCharacterPolicy.LatinOnly;

    /// <summary>Extra characters allowed through on top of the policy.</summary>
    public string AllowedExtraCharacters { get; set; } = string.Empty;

    public bool ReadAsYouType { get; set; }

    /// <summary>Whether clicking away from the window holds the reading where it got to.</summary>
    public bool PauseWhenUnfocused { get; set; }

    public string? OutputDirectory { get; set; }

    /// <summary>The file type Save writes. MP3 is smaller and plays anywhere.</summary>
    public AudioOutputFormat OutputFormat { get; set; } = AudioOutputFormat.Mp3;

    public int Mp3BitRate { get; set; } = 128000;

    public int MaxChunkLength { get; set; } = 2000;

    /// <summary>Whether a finished reading starts again, and whether the queue wraps.</summary>
    public RepeatMode Repeat { get; set; } = RepeatMode.Off;

    /// <summary>Whether the program asks at each start whether a newer release is published.</summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>Whether a newer version is put in a dialog at startup rather than left as a mark
    /// on the Updates tab.</summary>
    public bool PromptForUpdates { get; set; }

    /// <summary>When a check last succeeded, kept as a record of the last one that worked.</summary>
    public DateTime? LastUpdateCheckUtc { get; set; }

    /// <summary>A release the reader said no to, so the same one is not offered again.</summary>
    public int DismissedUpdateCode { get; set; }

    /// <summary>How big the text looks in the editor. Nothing that is saved changes.</summary>
    public double EditorFontSize { get; set; } = 14;

    /// <summary>Whether the alias list is applied to text on its way to the voice.</summary>
    public bool UseAliases { get; set; } = true;

    /// <summary>The lists that ship with the program which the reader has turned on. Empty to
    /// begin with, so none of them is applied.</summary>
    public List<string> AliasPacks { get; set; } = new();

    /// <summary>How a finished audio file announces itself.</summary>
    public WriteFinishedNotice WriteFinishedNotice { get; set; } = WriteFinishedNotice.Sound;

    /// <summary>Whether the voices Windows already has are offered alongside downloaded ones.</summary>
    public bool UseWindowsVoices { get; set; } = true;

    /// <summary>How many past messages the history keeps before the oldest falls off.</summary>
    public int KeepLastMessages { get; set; } = 200;

    /// <summary>How many messages one page of the history shows.</summary>
    public int ShowLastMessages { get; set; } = 10;

    /// <summary>Whether writing audio also keeps the words behind it as a script.</summary>
    public bool SaveScriptWithAudio { get; set; } = true;

    /// <summary>Whether closing a text tab with unsaved changes asks first.</summary>
    public bool WarnOnClosingUnsaved { get; set; } = true;

    /// <summary>Whether closing the window hides it in the notification area instead of quitting.</summary>
    public bool CloseToTray { get; set; }

    [JsonIgnore]
    public string ResolvedVoicesDirectory => InstallPaths.ResolveVoicesDirectory(
        VoicesDirectory,
        Environment.GetEnvironmentVariable(InstallPaths.VoicesEnvironmentVariable),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppDirectory,
        InstallPaths.HasVoices);

    /// <summary>The folder Save offers, which is a named folder under Music by default.</summary>
    [JsonIgnore]
    public string ResolvedOutputDirectory =>
        string.IsNullOrWhiteSpace(OutputDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), OutputFolderName)
            : OutputDirectory!;

    /// <summary>Name of the folder created under Music, so saved audio is not loose among it.</summary>
    public const string OutputFolderName = "TTS Util";

    /// <summary>Directory holding the running executable.</summary>
    public static string AppDirectory =>
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    /// <summary>True when the program is running from a portable folder.</summary>
    public static bool IsPortable => InstallPaths.IsPortable(AppDirectory, File.Exists);

    public static string SettingsPath
    {
        get
        {
            var path = InstallPaths.ResolveSettingsPath(
                AppDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                File.Exists);

            var directory = Path.GetDirectoryName(path);
            if (directory is not null) Directory.CreateDirectory(directory);
            return path;
        }
    }

    /// <summary>Where this instance was loaded from, and where Save writes back to.</summary>
    [JsonIgnore]
    public string? SourcePath { get; set; }

    public static AppSettings Load() => LoadFrom(SettingsPath);

    public static AppSettings LoadFrom(string path)
    {
        try
        {
            var settings = File.Exists(path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions) ?? new AppSettings()
                : new AppSettings();
            settings.SourcePath = path;
            return settings;
        }
        catch (Exception)
        {
            return new AppSettings { SourcePath = path };
        }
    }

    public void Save() => SaveTo(SourcePath ?? SettingsPath);

    /// <summary>Puts every setting back to what a first run would have, keeping the file it
    /// was loaded from, and writes it out.</summary>
    public void ResetToDefaults()
    {
        var fresh = new AppSettings();

        foreach (var property in typeof(AppSettings).GetProperties())
        {
            if (!property.CanWrite || property.Name == nameof(SourcePath)) continue;
            property.SetValue(this, property.GetValue(fresh));
        }

        Save();
    }

    public void SaveTo(string path)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception)
        {
            // Settings are a convenience; a read-only location is not fatal.
        }
    }

    /// <summary>The speaker last used for a voice, falling back to the shared setting.</summary>
    public int GetSpeakerId(string? voiceName)
    {
        var key = VoiceKey(voiceName);
        if (key is not null && SpeakerIds.TryGetValue(key, out var id)) return id;
        return SpeakerId;
    }

    /// <summary>Records the speaker for one voice, and as the fallback for a new voice.</summary>
    public void SetSpeakerId(string? voiceName, int speakerId)
    {
        SpeakerId = speakerId;

        var key = VoiceKey(voiceName);
        if (key is null) return;
        SpeakerIds[key] = speakerId;
    }

    /// <summary>The starred speakers for a voice, in the order they were starred.</summary>
    public IReadOnlyList<int> GetFavouriteSpeakers(string? voiceName)
    {
        var key = VoiceKey(voiceName);
        if (key is null || !FavouriteSpeakers.TryGetValue(key, out var ids)) return Array.Empty<int>();
        return ids;
    }

    /// <summary>The starred voices, as lower case voice names.</summary>
    public List<string> FavouriteVoices { get; set; } = new();

    public bool IsFavouriteVoice(string? voiceName)
    {
        var key = VoiceKey(voiceName);
        return key is not null && FavouriteVoices.Contains(key);
    }

    /// <summary>Stars or unstars a voice, returning whether it is starred afterwards.</summary>
    public bool ToggleFavouriteVoice(string? voiceName)
    {
        var key = VoiceKey(voiceName);
        if (key is null || FavouriteVoices.Remove(key)) return false;

        FavouriteVoices.Add(key);
        return true;
    }

    public bool IsFavouriteSpeaker(string? voiceName, int speakerId) =>
        GetFavouriteSpeakers(voiceName).Contains(speakerId);

    /// <summary>Stars or unstars a speaker, returning whether it is starred afterwards.</summary>
    public bool ToggleFavouriteSpeaker(string? voiceName, int speakerId)
    {
        var key = VoiceKey(voiceName);
        if (key is null) return false;

        if (!FavouriteSpeakers.TryGetValue(key, out var ids))
        {
            ids = new List<int>();
            FavouriteSpeakers[key] = ids;
        }

        if (ids.Remove(speakerId))
        {
            // An empty list would only grow the settings file with nothing in it.
            if (ids.Count == 0) FavouriteSpeakers.Remove(key);
            return false;
        }

        ids.Add(speakerId);
        return true;
    }

    private static string? VoiceKey(string? voiceName) =>
        string.IsNullOrWhiteSpace(voiceName) ? null : voiceName!.Trim().ToLowerInvariant();

    public ChunkerOptions ToChunkerOptions() => new()
    {
        MaxChunkLength = MaxChunkLength,
        ScaleSilenceToRate = ScaleSilenceToRate,
        SpeechRate = Speed,
        Silence = new SilenceOptions
        {
            LineEndingMs = SilenceLineEndingMs,
            SentenceMs = SilenceSentenceMs,
            QuestionMs = SilenceQuestionMs,
            ExclamationMs = SilenceExclamationMs,
        },
        Filters = new TextFilterOptions
        {
            FilterHashes = FilterHashes,
            FilterWebLinks = FilterWebLinks,
            FilterMailToLinks = FilterMailToLinks,
        },
        Spoken = new SpokenCharacterOptions
        {
            Policy = SpokenCharacters,
            AllowedExtra = AllowedExtraCharacters ?? string.Empty,
        },
    };
}
