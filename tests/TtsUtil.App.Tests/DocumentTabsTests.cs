using System.IO;
using System.Windows;
using System.Windows.Threading;
using TtsUtil.Core.Settings;
using Xunit;

namespace TtsUtil.App.Tests;

[Collection("wpf")]
public sealed class DocumentTabsTests : IDisposable
{
    private readonly WpfFixture _wpf;
    private readonly string _root;

    public DocumentTabsTests(WpfFixture wpf)
    {
        _wpf = wpf;
        _root = Path.Combine(Path.GetTempPath(), "TtsUtilWinDocuments", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "voices"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void EachTabKeepsItsOwnWordsAndScript()
    {
        var window = CreateWindow();

        _wpf.Invoke(() =>
        {
            window.InputText.Text = "The first one.";
            window.TextScriptTitle = "Act One";

            var second = window.NewDocument();
            window.DocumentTabs.SelectedItem = second.Tab;
            window.InputText.Text = "The second one.";

            Assert.Null(window.TextScriptTitle);
            Assert.Equal("The first one.", window.Documents[0].Box.Text);
            Assert.Equal("The second one.", window.InputText.Text);

            window.Close();
        });
    }

    /// <summary>An untouched tab shows its own name in grey, and keeps showing the right one
    /// after the tab is renamed.</summary>
    [Fact]
    public void AnEmptyTabWearsItsOwnNameAsTheHint()
    {
        var window = CreateWindow();

        _wpf.Invoke(() =>
        {
            var second = window.NewDocument();

            Assert.Equal("Text 1", window.Documents[0].Box.Tag);
            Assert.Equal("Text 2", second.Box.Tag);

            window.DocumentTabs.SelectedItem = second.Tab;
            window.TextScriptTitle = "Act Two";

            Assert.Equal("Act Two", second.Box.Tag);

            window.Close();
        });
    }

    /// <summary>The title box holds the name of the tab in front and nothing else, so a new tab
    /// cannot be saved over the script the last tab came from.</summary>
    [Fact]
    public void TheTitleBoxFollowsTheTabInFront()
    {
        var window = CreateWindow();

        _wpf.Invoke(() =>
        {
            window.TextTitleBox.Text = "Act One";

            var second = window.NewDocument();
            window.DocumentTabs.SelectedItem = second.Tab;

            Assert.Equal(string.Empty, window.TextTitleBox.Text);

            window.TextTitleBox.Text = "Act Two";
            window.DocumentTabs.SelectedItem = window.Documents[0].Tab;

            Assert.Equal("Act One", window.TextTitleBox.Text);

            window.DocumentTabs.SelectedItem = second.Tab;

            Assert.Equal("Act Two", window.TextTitleBox.Text);

            window.Close();
        });
    }

    /// <summary>Eight tabs stay on one line, so closing one cannot reshuffle the rows.</summary>
    [Fact]
    public void TheTabStripIsOneRowThatScrollsSideways()
    {
        var window = CreateWindow();

        _wpf.Invoke(() =>
        {
            for (var more = 0; more < 7; more++) window.NewDocument();

            window.DocumentTabs.ApplyTemplate();
            var strip = window.DocumentTabs.Template.FindName("HeaderScroller", window.DocumentTabs);
            var scroller = Assert.IsType<System.Windows.Controls.ScrollViewer>(strip);
            var panel = Assert.IsType<System.Windows.Controls.StackPanel>(scroller.Content);

            Assert.Equal(System.Windows.Controls.Orientation.Horizontal, panel.Orientation);
            Assert.Equal(System.Windows.Controls.ScrollBarVisibility.Disabled, scroller.VerticalScrollBarVisibility);

            window.CloseDocument(window.Documents[3]);
            Assert.Equal(7, window.Documents.Count);

            window.Close();
        });
    }

    [Fact]
    public void ClosingTheLastTabEmptiesItInstead()
    {
        var window = CreateWindow();

        _wpf.Invoke(() =>
        {
            window.UnsavedCloseAsker = _ => (Close: true, StopAsking: false);
            window.InputText.Text = "Something to be rid of.";
            window.TextScriptTitle = "Act One";
            window.TextTitleBox.Text = "Act One";
            window.CloseDocument(window.ActiveDocument!);

            Assert.Single(window.Documents);
            Assert.Equal(string.Empty, window.InputText.Text);
            Assert.Null(window.TextScriptTitle);
            Assert.Equal(string.Empty, window.TextTitleBox.Text);

            window.Close();
        });
    }

    [Fact]
    public void PickingThePlusOpensATabAndBringsItForward()
    {
        var window = CreateWindow();

        _wpf.Invoke(() =>
        {
            window.DocumentTabs.SelectedItem = window.DocumentTabs.Items[window.DocumentTabs.Items.Count - 1];

            Assert.Equal(2, window.Documents.Count);
            Assert.Same(window.Documents[1], window.ActiveDocument);

            window.CloseDocument(window.Documents[1]);
            Assert.Single(window.Documents);
            Assert.Same(window.Documents[0], window.ActiveDocument);

            window.Close();
        });
    }

    /// <summary>Tabs share the strip evenly, up to a width of their own and down to the width
    /// that starts the strip scrolling.</summary>
    [Fact]
    public void TabsShareTheStripEvenlyBetweenTheTwoLimits()
    {
        Assert.Equal(180, TabStripSizing.TabWidth(600, 1, 180, 80));
        Assert.Equal(150, TabStripSizing.TabWidth(300, 2, 180, 80));
        Assert.Equal(80, TabStripSizing.TabWidth(600, 20, 180, 80));
    }

    /// <summary>A header laid out by the tab, rather than by itself, keeps the name at the left
    /// and the close button at the right edge however wide the tab grows.</summary>
    [Fact]
    public void ATabHeaderFillsTheTabItSitsIn()
    {
        var window = CreateWindow();

        _wpf.Invoke(() =>
        {
            var widest = TabStripSizing.GetWidest(window.DocumentTabs);

            window.Show();
            window.Documents[0].Tab.Width = widest;
            window.UpdateLayout();

            var header = (FrameworkElement)window.Documents[0].Tab.Header;
            Assert.True(header.ActualWidth > widest - 30,
                $"the header was {header.ActualWidth} wide in a {widest} wide tab");

            window.Close();
        });
    }

    /// <summary>A tab that has just opened is sized with the rest at once, rather than sitting at
    /// the width of its own name until the pointer leaves the strip.</summary>
    [Fact]
    public void ANewTabIsSizedAsSoonAsItOpens()
    {
        var window = CreateWindow();

        _wpf.Invoke(() =>
        {
            window.Width = 1000;
            window.Show();
            window.UpdateLayout();

            var second = window.NewDocument();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            Assert.InRange(second.Tab.Width, TabStripSizing.GetNarrowest(window.DocumentTabs),
                TabStripSizing.GetWidest(window.DocumentTabs));
            Assert.Equal(window.Documents[0].Tab.Width, second.Tab.Width);

            window.Close();
        });
    }

    private MainWindow CreateWindow() => _wpf.Invoke(() =>
    {
        var settings = AppSettings.LoadFrom(Path.Combine(_root, Guid.NewGuid().ToString("N") + ".json"));
        settings.VoicesDirectory = Path.Combine(_root, "voices");
        settings.UseWindowsVoices = false;

        return new MainWindow(settings, loadVoiceOnSelection: false);
    });
}
