/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Globalization;
using System.Text;

namespace TtsUtil.Core.Text;

/// <summary>Which characters survive as speech. Delimiters already became silence by now.</summary>
public enum SpokenCharacterPolicy
{
    /// <summary>Everything reaches the engine, which is how earlier versions behaved.</summary>
    Off,

    /// <summary>Only a-z, A-Z, 0-9 and the punctuation that phrases speech.</summary>
    LatinOnly,

    /// <summary>Any Unicode letter or digit, keeping accents and non Latin scripts.</summary>
    AnyLetter,
}

public sealed class SpokenCharacterOptions
{
    public SpokenCharacterPolicy Policy { get; set; } = SpokenCharacterPolicy.LatinOnly;

    /// <summary>Extra characters the user allows through, on top of the policy.</summary>
    public string AllowedExtra { get; set; } = string.Empty;

    public SpokenCharacterOptions Clone() => new()
    {
        Policy = Policy,
        AllowedExtra = AllowedExtra,
    };
}

/// <summary>Strips characters the engine would voice as symbol names.</summary>
public static class SpokenCharacters
{
    /// <summary>Removed rather than spaced when between letters, so "don't" stays one word.</summary>
    private const string Joiners = "'’ʼ´";

    /// <summary>Punctuation the engine phrases on rather than voices, so it always survives.</summary>
    private const string Prosody = ".,;:?!…。，；：？！";

    /// <summary>Rewrites the buffer in place, returning how many characters it lost. Spaces it
    /// gathers up or trims are not counted.</summary>
    public static int Apply(List<char> buffer, SpokenCharacterOptions options)
    {
        if (options.Policy == SpokenCharacterPolicy.Off || buffer.Count == 0) return 0;

        var before = Visible(buffer);
        var output = new List<char>(buffer.Count);

        for (var index = 0; index < buffer.Count; index++)
        {
            var c = buffer[index];

            if (char.IsWhiteSpace(c))
            {
                // Runs of whitespace, including any this pass introduced, read as one gap.
                if (output.Count > 0 && !char.IsWhiteSpace(output[^1])) output.Add(c);
                continue;
            }

            if (IsAllowed(c, options))
            {
                // A gap the pass introduced before punctuation would be heard as a stumble.
                if (Prosody.IndexOf(c) >= 0 && output.Count > 0 && char.IsWhiteSpace(output[^1]))
                {
                    output.RemoveAt(output.Count - 1);
                }

                output.Add(c);
                continue;
            }

            var folded = Fold(c, options);
            if (folded is { } baseLetter)
            {
                output.Add(baseLetter);
                continue;
            }

            // A joiner inside a word disappears; anything else leaves a gap between words.
            if (Joiners.IndexOf(c) >= 0 && IsBetweenAllowed(buffer, index, options)) continue;

            if (output.Count > 0 && !char.IsWhiteSpace(output[^1])) output.Add(' ');
        }

        while (output.Count > 0 && char.IsWhiteSpace(output[^1])) output.RemoveAt(output.Count - 1);

        buffer.Clear();
        buffer.AddRange(output);
        return before - Visible(buffer);
    }

    private static int Visible(List<char> buffer) => buffer.Count(c => !char.IsWhiteSpace(c));

    /// <summary>Applies the policy to a string, which is what tests and callers usually want.</summary>
    public static string Clean(string text, SpokenCharacterOptions options)
    {
        var buffer = new List<char>(text);
        Apply(buffer, options);
        return new string(buffer.ToArray());
    }

    public static bool IsAllowed(char c, SpokenCharacterOptions options)
    {
        if (options.AllowedExtra.IndexOf(c) >= 0) return true;
        if (Prosody.IndexOf(c) >= 0) return true;

        return IsWordCharacter(c, options.Policy);
    }

    /// <summary>A letter or digit under the policy, which is what a joiner must sit between.</summary>
    public static bool IsWordCharacter(char c, SpokenCharacterPolicy policy) => policy switch
    {
        SpokenCharacterPolicy.LatinOnly => IsLatinLetterOrDigit(c),
        SpokenCharacterPolicy.AnyLetter => char.IsLetterOrDigit(c),
        _ => true,
    };

    /// <summary>Strips the accent off a Latin letter, so cafe survives where caf would not.</summary>
    private static char? Fold(char c, SpokenCharacterOptions options)
    {
        if (options.Policy != SpokenCharacterPolicy.LatinOnly) return null;
        if (!char.IsLetter(c)) return null;

        foreach (var part in c.ToString().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(part) == UnicodeCategory.NonSpacingMark) continue;
            if (IsLatinLetterOrDigit(part)) return part;
            break;
        }

        return null;
    }

    private static bool IsLatinLetterOrDigit(char c) =>
        (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');

    private static bool IsBetweenAllowed(List<char> buffer, int index, SpokenCharacterOptions options)
    {
        if (index == 0 || index == buffer.Count - 1) return false;
        return IsWordCharacter(buffer[index - 1], options.Policy) &&
               IsWordCharacter(buffer[index + 1], options.Policy);
    }
}
