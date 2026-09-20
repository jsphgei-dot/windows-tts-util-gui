/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace TtsUtil.App;

/// <summary>Asks for the name a script is saved under, so Ctrl+S never saves under a stale title.</summary>
public partial class NameScriptWindow : Window
{
    private readonly string _fallback;

    public NameScriptWindow(string suggested)
    {
        InitializeComponent();
        _fallback = suggested.Trim();
        NameBox.Text = suggested;
        NameBox.SelectAll();
        Loaded += (_, _) => NameBox.Focus();

        if (_fallback.Length == 0) return;

        NameBox.Tag = _fallback;
        HintText.Text = $"Left empty it saves as {_fallback}. A name already in the library is replaced.";
    }

    /// <summary>The name that was typed, or null when the dialog was dismissed.</summary>
    public string? ChosenName { get; private set; }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0) name = _fallback;

        if (name.Length == 0)
        {
            HintText.Text = "Type a name first.";
            return;
        }

        ChosenName = name;
        DialogResult = true;
    }

    private void OnNameKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) OnSave(sender, e);
    }
}
