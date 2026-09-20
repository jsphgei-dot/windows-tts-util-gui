/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using MouseWheelEventArgs = System.Windows.Input.MouseWheelEventArgs;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using SystemColors = System.Windows.SystemColors;
using TextBox = System.Windows.Controls.TextBox;

namespace TtsUtil.App;

/// <summary>The Text tab holds several documents, one tab each, so audio can be written from one
/// while another is being written or read.</summary>
public partial class MainWindow
{
    private readonly List<TextDocument> _documents = new();
    private TabItem? _plusTab;
    private ScrollViewer? _tabStrip;

    /// <summary>The open documents, in tab order.</summary>
    internal IReadOnlyList<TextDocument> Documents => _documents;

    /// <summary>The document the user is looking at, or null before the window is built.</summary>
    internal TextDocument? ActiveDocument => (DocumentTabs?.SelectedItem as TabItem)?.Tag as TextDocument;

    /// <summary>The text box of the open document, which the rest of the window edits.</summary>
    internal TextBox InputText => ActiveDocument?.Box!;

    /// <summary>The script the open document holds, which names audio written from it.</summary>
    private string? ActiveScriptTitle
    {
        get => ActiveDocument?.ScriptTitle;
        set
        {
            var document = ActiveDocument;
            if (document is null) return;

            document.ScriptTitle = value;
            if (value is not null) Rename(document, value);
        }
    }

    /// <summary>Adds a document and gives it the editor's current size and wrapping.</summary>
    internal TextDocument NewDocument(string? title = null, string text = "", string? scriptTitle = null)
    {
        var box = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = WrapTextBox?.IsChecked == false ? TextWrapping.NoWrap : TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = WrapTextBox?.IsChecked == false
                ? ScrollBarVisibility.Auto
                : ScrollBarVisibility.Disabled,
            FontSize = EditorFontSizeBox?.SelectedItem as double? ?? EditorFontSizes[1],
            Padding = new Thickness(6),
            Text = text,
        };
        box.TextChanged += OnInputTextChanged;

        var tab = new TabItem { Content = box };
        var document = new TextDocument(tab, box, title ?? NextDocumentTitle())
        {
            ScriptTitle = scriptTitle,
            TitleInBox = scriptTitle ?? string.Empty,
        };
        tab.Tag = document;
        tab.Header = BuildTabHeader(document);
        document.SavedText = text;

        EnsurePlusTab();
        _documents.Add(document);
        DocumentTabs.Items.Insert(DocumentTabs.Items.Count - 1, tab);
        TabStripSizing.SizeNow(DocumentTabs);

        if (DocumentTabs.SelectedItem is null || ReferenceEquals(DocumentTabs.SelectedItem, _plusTab))
        {
            DocumentTabs.SelectedItem = tab;
        }

        ScrollTabsToEnd();
        return document;
    }

    /// <summary>The document a text box belongs to, or null for a box outside the tabs.</summary>
    internal TextDocument? DocumentFor(object? box) =>
        _documents.FirstOrDefault(document => ReferenceEquals(document.Box, box));

    /// <summary>A tab wears a star while its words differ from the last save, so unsaved work is
    /// visible without opening the tab.</summary>
    internal void ShowTabTitle(TextDocument document)
    {
        var unsaved = document.Unsaved;
        document.Label.Text = unsaved ? document.Title + " *" : document.Title;
        document.Label.ToolTip = unsaved
            ? $"{document.Title} (changes not saved to a script)"
            : document.Title;
    }

    /// <summary>Takes the star off a document whose words have just been written to a script.</summary>
    internal void MarkDocumentSaved(TextDocument? document)
    {
        if (document is null) return;

        document.SavedText = document.Box.Text;
        ShowTabTitle(document);
    }

    /// <summary>Marks the Text tab when tabs arrive while another tab is in front, so they are
    /// not missed. Opening while the Text tab is already up needs no mark.</summary>
    internal void MarkNewTabs(int count)
    {
        if (ReferenceEquals(Tabs.SelectedItem, TextTab)) return;

        TextTabMark.ToolTip = count == 1 ? "New tab opened" : "New tabs opened";
        TextTabMark.Visibility = Visibility.Visible;
    }

    /// <summary>The Text tab coming up is the news being read, so the mark goes.</summary>
    private void OnMainTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, Tabs)) return;
        if (ReferenceEquals(Tabs.SelectedItem, TextTab)) TextTabMark.Visibility = Visibility.Collapsed;
    }

    /// <summary>Closes a document. The last one is emptied rather than taken away.</summary>
    internal void CloseDocument(TextDocument document)
    {
        if (_busy && ReferenceEquals(document, _runDocument))
        {
            SetStatus("Press Stop before closing this tab.");
            return;
        }

        if (!AgreedToLoseChanges(document)) return;

        CancelWriteFor(document);

        if (_documents.Count == 1)
        {
            document.Box.Clear();
            document.ScriptTitle = null;
            document.TitleInBox = string.Empty;
            ShowTitleOfActiveTab();
            Rename(document, "Text 1");
            MarkDocumentSaved(document);
            return;
        }

        var index = _documents.IndexOf(document);
        _documents.Remove(document);

        // Picking the neighbour first keeps the plus from being selected, and opening a tab.
        DocumentTabs.SelectedItem = _documents[Math.Min(index, _documents.Count - 1)].Tab;
        DocumentTabs.Items.Remove(document.Tab);
        TabStripSizing.Size(DocumentTabs);
    }

    private void OnNewDocument(object sender, RoutedEventArgs e)
    {
        var document = NewDocument();
        DocumentTabs.SelectedItem = document.Tab;
        document.Box.Focus();
    }

    /// <summary>The line list and the script name follow whichever document is in front.</summary>
    private void OnDocumentTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, DocumentTabs)) return;

        if (ReferenceEquals(DocumentTabs.SelectedItem, _plusTab) && _plusTab is not null)
        {
            var added = NewDocument();
            DocumentTabs.SelectedItem = added.Tab;
            added.Box.Focus();
            return;
        }

        if (DocumentTabs.SelectedItem is TabItem selected) ScrollTabIntoView(selected);

        if (_initialising || InputText is null) return;

        _spokenLine = -1;

        // Another tab holds other text, so the stopped spot no longer points anywhere useful.
        RememberStoppedSpot(null);
        RebuildLineList();
        ShowTitleOfActiveTab();
    }

    /// <summary>The title box belongs to the tab in front, so it holds that tab's name and
    /// nothing else. Another tab's name is never left sitting there to be saved over.</summary>
    private void ShowTitleOfActiveTab() =>
        TextTitleBox.Text = ActiveDocument?.TitleInBox ?? string.Empty;

    private void OnTitleBoxChanged(object sender, TextChangedEventArgs e)
    {
        if (ActiveDocument is TextDocument document) document.TitleInBox = TextTitleBox.Text;
    }

    /// <summary>A box being spoken or written is held still, and looks it. It can still be
    /// scrolled and copied from.</summary>
    private static void LockBox(TextBox box, bool locked)
    {
        box.IsReadOnly = locked;
        box.Background = locked ? SystemColors.ControlBrush : SystemColors.WindowBrush;
        box.Foreground = locked ? SystemColors.GrayTextBrush : SystemColors.WindowTextBrush;
    }

    /// <summary>Brings a tab into view on a strip that is wider than the window.</summary>
    internal void ScrollTabIntoView(TabItem tab)
    {
        TabStrip();
        Dispatcher.BeginInvoke(new Action(() => tab.BringIntoView()), DispatcherPriority.Loaded);
    }

    /// <summary>Walks the strip to its right end, where the newest tab and the plus sit, so the
    /// plus can be pressed again without scrolling.</summary>
    internal void ScrollTabsToEnd()
    {
        if (TabStrip() is not ScrollViewer strip) return;

        Dispatcher.BeginInvoke(new Action(strip.ScrollToRightEnd), DispatcherPriority.Loaded);
    }

    /// <summary>The sideways scroller the tab headers sit in, once the template has been built.</summary>
    private ScrollViewer? TabStrip()
    {
        if (_tabStrip is not null) return _tabStrip;

        _tabStrip = TabStripSizing.Strip(DocumentTabs);
        if (_tabStrip is not null) _tabStrip.PreviewMouseWheel += OnTabStripWheel;

        return _tabStrip;
    }

    /// <summary>The wheel walks the strip sideways, since it has nowhere to go up or down.</summary>
    private void OnTabStripWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer strip) return;

        strip.ScrollToHorizontalOffset(strip.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    /// <summary>The plus sits at the end of the strip, and opens a tab when it is picked.</summary>
    private void EnsurePlusTab()
    {
        if (_plusTab is not null) return;

        _plusTab = new TabItem
        {
            Header = new TextBlock { Text = "+", FontWeight = FontWeights.Bold, Margin = new Thickness(4, 0, 4, 0) },
            ToolTip = "Another text tab, written to audio alongside this one",
        };
        TabStripSizing.SetShares(_plusTab, false);

        DocumentTabs.Items.Add(_plusTab);
    }

    /// <summary>Asks before a tab with unsaved words goes, unless the reader has turned the
    /// question off.</summary>
    private bool AgreedToLoseChanges(TextDocument document)
    {
        if (!document.Unsaved || !_settings.WarnOnClosingUnsaved) return true;

        var answer = UnsavedCloseAsker($"{document.Title} has changes that are not saved as a " +
                                       "script. Close it anyway?");
        if (answer.StopAsking)
        {
            _settings.WarnOnClosingUnsaved = false;
            WarnOnClosingUnsavedBox.IsChecked = false;
            _settings.Save();
        }

        return answer.Close;
    }

    private void Rename(TextDocument document, string title)
    {
        document.Title = title;
        ShowTabTitle(document);
    }

    /// <summary>Numbers a new document past the ones already open.</summary>
    private string NextDocumentTitle()
    {
        for (var number = 1; ; number++)
        {
            var title = "Text " + number;
            if (!_documents.Any(document => document.Title == title)) return title;
        }
    }

    private object BuildTabHeader(TextDocument document)
    {
        var close = new Button
        {
            Content = "×",
            Style = (Style)FindResource("TabCloseButton"),
            Focusable = false,
            ToolTip = "Close this text tab",
        };
        close.Click += (_, _) => CloseDocument(document);
        DockPanel.SetDock(close, Dock.Right);

        var panel = new DockPanel();
        panel.Children.Add(close);
        panel.Children.Add(document.Label);
        return panel;
    }
}

/// <summary>One text document, its tab and the box the words are typed into.</summary>
internal sealed class TextDocument
{
    public TextDocument(TabItem tab, TextBox box, string title)
    {
        Tab = tab;
        Box = box;
        Title = title;
        box.VerticalContentAlignment = VerticalAlignment.Top;
        Label = new TextBlock
        {
            Text = title,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
    }

    public TabItem Tab { get; }

    public TextBox Box { get; }

    public TextBlock Label { get; }

    /// <summary>The name on the tab. An empty box wears it in grey until it is typed into.</summary>
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            Box.Tag = value;
        }
    }

    private string _title = string.Empty;

    /// <summary>The words as they were at the last save, which the star is measured against.</summary>
    public string SavedText { get; set; } = string.Empty;

    /// <summary>Whether the box has moved on from what was last written to a script.</summary>
    public bool Unsaved => !string.Equals(Box.Text, SavedText, StringComparison.Ordinal);

    /// <summary>The script this document came from, which names audio written from it.</summary>
    public string? ScriptTitle { get; set; }

    /// <summary>The name in the title box while this tab is in front. A tab that came from
    /// nowhere starts empty, so saving it cannot land on another script by accident.</summary>
    public string TitleInBox { get; set; } = string.Empty;
}
