/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using TtsUtil.Core.Text;

namespace TtsUtil.App;

/// <summary>One row of the line list, numbered as the editor numbers it.</summary>
public sealed class ScriptLineRow
{
    public ScriptLineRow(LineSpan line, int numberWidth = 0)
    {
        Index = line.Number;
        Start = line.Start;
        Text = line.Text;
        Number = $"{Index + 1}.".PadLeft(numberWidth + 1);
    }

    public int Index { get; }

    public int Start { get; }

    public string Text { get; }

    /// <summary>The line number, padded so every row's text starts in the same column.</summary>
    public string Number { get; }

    public string Body => Text.Trim();

    public string Description => ToString();

    /// <summary>What the list shows, and what a screen reader announces.</summary>
    public override string ToString()
    {
        var body = Text.Trim();
        return body.Length == 0 ? $"{Index + 1}." : $"{Index + 1}.  {body}";
    }
}
