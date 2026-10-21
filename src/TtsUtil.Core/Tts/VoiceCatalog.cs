/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

namespace TtsUtil.Core.Tts;

/// <summary>Finds sherpa voice models inside a directory of model folders.</summary>
public static class VoiceCatalog
{
    private static readonly string[] VocoderMarkers = { "vocos", "hifigan", "vocoder" };

    /// <summary>Returns every usable voice directory under the given root, oldest folder first,
    /// which is the order they were downloaded in.</summary>
    public static IReadOnlyList<VoiceDescriptor> Scan(string rootDirectory)
    {
        var results = new List<VoiceDescriptor>();
        if (!Directory.Exists(rootDirectory)) return results;

        foreach (var dir in Directory.EnumerateDirectories(rootDirectory))
        {
            var voice = TryLoad(dir);
            if (voice is not null) results.Add(voice);
        }

        results.Sort((a, b) =>
        {
            var installed = a.InstalledUtc.CompareTo(b.InstalledUtc);
            return installed != 0 ? installed : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        return results;
    }

    /// <summary>Inspects one directory, returning null when it holds no usable model.</summary>
    public static VoiceDescriptor? TryLoad(string directory)
    {
        if (!Directory.Exists(directory)) return null;

        var tokens = FindFile(directory, "tokens.txt");
        if (tokens is null) return null;

        var onnxFiles = Directory.GetFiles(directory, "*.onnx", SearchOption.TopDirectoryOnly);
        if (onnxFiles.Length == 0) return null;

        var name = new DirectoryInfo(directory).Name;
        var dataDir = FindDirectory(directory, "espeak-ng-data");
        var dictDir = FindDirectory(directory, "dict");
        var lexicon = FindFile(directory, "lexicon.txt");
        var license = FindFile(directory, "LICENSE") ?? FindFile(directory, "LICENSE.txt");
        var modelCard = FindFile(directory, "MODEL_CARD") ?? FindFile(directory, "MODEL_CARD.md");

        var voicesBin = FindFile(directory, "voices.bin");
        var vocoder = onnxFiles.FirstOrDefault(IsVocoder);

        VoiceModelKind kind;
        string model;

        if (voicesBin is not null)
        {
            model = onnxFiles.FirstOrDefault(f => !IsVocoder(f)) ?? onnxFiles[0];
            kind = IsKitten(model) ? VoiceModelKind.Kitten : VoiceModelKind.Kokoro;
        }
        else if (vocoder is not null && onnxFiles.Length > 1)
        {
            kind = VoiceModelKind.Matcha;
            model = onnxFiles.First(f => !IsVocoder(f));
        }
        else
        {
            kind = VoiceModelKind.Vits;
            model = onnxFiles.OrderByDescending(f => new FileInfo(f).Length).First();
            vocoder = null;
        }

        return new VoiceDescriptor
        {
            Name = name,
            Directory = directory,
            InstalledUtc = System.IO.Directory.GetCreationTimeUtc(directory),
            Kind = kind,
            ModelPath = model,
            TokensPath = tokens,
            VocoderPath = kind == VoiceModelKind.Matcha ? vocoder : null,
            VoicesBinPath = voicesBin,
            LexiconPath = lexicon,
            DataDirPath = dataDir,
            DictDirPath = dictDir,
            LicensePath = license,
            ModelCardPath = modelCard,
        };
    }

    /// <summary>Kitten ships a voices.bin like Kokoro but loads differently, which only the
    /// model's own model_type tells apart.</summary>
    private static bool IsKitten(string model) =>
        OnnxMetadata.Read(model).TryGetValue("model_type", out var type)
            ? type.StartsWith("kitten", StringComparison.OrdinalIgnoreCase)
            : Path.GetFileName(Path.GetDirectoryName(model) ?? string.Empty)
                .StartsWith("kitten", StringComparison.OrdinalIgnoreCase);

    private static bool IsVocoder(string path)
    {
        var file = Path.GetFileName(path).ToLowerInvariant();
        return VocoderMarkers.Any(marker => file.Contains(marker, StringComparison.Ordinal));
    }

    private static string? FindFile(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        return File.Exists(path) ? path : null;
    }

    private static string? FindDirectory(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        return Directory.Exists(path) ? path : null;
    }
}
