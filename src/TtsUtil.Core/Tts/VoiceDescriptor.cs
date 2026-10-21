/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

namespace TtsUtil.Core.Tts;

public enum VoiceModelKind
{
    Vits,
    Matcha,
    Kokoro,
    Kitten,
}

/// <summary>Where a voice comes from.</summary>
public enum VoiceSource
{
    /// <summary>A downloaded sherpa model directory.</summary>
    Sherpa,

    /// <summary>A speech voice Windows already has.</summary>
    Windows,
}

/// <summary>A voice that can be loaded. The path properties belong to sherpa models and are
/// empty for a Windows voice, which carries an Id instead.</summary>
public sealed class VoiceDescriptor
{
    public string Name { get; init; } = string.Empty;

    public VoiceSource Source { get; init; } = VoiceSource.Sherpa;

    /// <summary>What Windows calls this voice. Empty for a sherpa model.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Shown beside the name in the picker, for a Windows voice.</summary>
    public string Language { get; init; } = string.Empty;

    public string Directory { get; init; } = string.Empty;

    /// <summary>When the model folder appeared, which orders the picker by download.</summary>
    public DateTime InstalledUtc { get; init; }

    public VoiceModelKind Kind { get; init; }

    /// <summary>The acoustic model, or the single model for vits and kokoro.</summary>
    public string ModelPath { get; init; } = string.Empty;

    public string TokensPath { get; init; } = string.Empty;

    /// <summary>Vocoder model, used by matcha voices only.</summary>
    public string? VocoderPath { get; init; }

    /// <summary>Speaker embedding file, used by kokoro voices only.</summary>
    public string? VoicesBinPath { get; init; }

    public string? LexiconPath { get; init; }

    /// <summary>espeak-ng data directory, when the model needs one.</summary>
    public string? DataDirPath { get; init; }

    /// <summary>jieba dictionary directory, for Chinese models.</summary>
    public string? DictDirPath { get; init; }

    public string? LicensePath { get; init; }

    public string? ModelCardPath { get; init; }

    public override string ToString() => Name;
}
