using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TtsUtil.Core.Settings;
using TtsUtil.Core.Tts;
using Xunit;

namespace TtsUtil.App.Tests;

[Collection("wpf")]
public sealed class LineListTests : IDisposable
{
    private readonly WpfFixture _wpf;
    private readonly string _root;
    private readonly string _voicesDir;
    private readonly string _settingsPath;
    private readonly List<MainWindow> _windows = new();

    public LineListTests(WpfFixture wpf)
    {
        _wpf = wpf;
        _root = Path.Combine(Path.GetTempPath(), "TtsUtilWinLines", Guid.NewGuid().ToString("N"));
        _voicesDir = Path.Combine(_root, "voices");
        _settingsPath = Path.Combine(_root, "settings.json");
        Directory.CreateDirectory(_voicesDir);
    }

    public void Dispose()
    {
        _wpf.Invoke(() =>
        {
            foreach (var window in _windows) window.Close();
        });

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void TheListNumbersLinesTheWayTheEditorDoes()
    {
        var window = CreateWindow();

        _wpf.Invoke(() =>
        {
            window.InputText.Text = "First line.\r\nSecond line.\r\n\r\nFourth line.";
            window.RebuildLineList();

            var rows = window.LineList.Items.Cast<ScriptLineRow>().Select(r => r.ToString()).ToList();
            Assert.Equal(new[] { "1.  First line.", "2.  Second line.", "3.", "4.  Fourth line." }, rows);
        });
    }

    [Fact]
    public void ReadingFromASelectedLineSkipsWhatCameBefore()
    {
        var window = CreateWindow();

        _wpf.Invoke(() =>
        {
            window.InputText.Text = "Skip me.\nRead this.";
            window.RebuildLineList();
            window.LineList.SelectedIndex = 1;
        });

        Click(window.ReadFromHereButton);

        // "Skip me." plus its line break is nine characters, so the run begins at nine.
        _wpf.Invoke(() => Assert.Equal(9, window.LastReadStartOffset));
    }

    [Fact]
    public void ReadingFromTheLastLineOfBlankTextSaysThereIsNothingLeft()
    {
        var window = CreateWindow();

        _wpf.Invoke(() =>
        {
            window.InputText.Text = "Words here.\n\n   \n";
            window.RebuildLineList();
            window.LineList.SelectedIndex = 1;
        });

        Click(window.ReadFromHereButton);

        _wpf.Invoke(() => Assert.Equal("There is nothing left to read from there.", window.StatusText.Text));
    }

    [Fact]
    public void WithNoSelectionTheCaretDecidesWhereToStart()
    {
        var window = CreateWindow();

        _wpf.Invoke(() =>
        {
            window.InputText.Text = "One.\nTwo.\nThree.";
            window.RebuildLineList();
            window.LineList.SelectedIndex = -1;
            window.InputText.CaretIndex = 6;
        });

        Click(window.ReadFromHereButton);

        // The caret sits inside line two, which starts at offset five, not at the caret.
        _wpf.Invoke(() => Assert.Equal(5, window.LastReadStartOffset));
    }

    [Fact]
    public void HidingTheListEmptiesItAndCollapsesTheColumn()
    {
        var window = CreateWindow();

        _wpf.Invoke(() =>
        {
            window.InputText.Text = "One.\nTwo.";
            window.RebuildLineList();
            Assert.Equal(2, window.LineList.Items.Count);

            window.ShowLinesBox.IsChecked = false;

            Assert.Equal(Visibility.Collapsed, window.LinePanel.Visibility);
            Assert.Empty(window.LineList.Items);
            Assert.Equal(0, window.LineListColumn.Width.Value);
        });
    }

    [Fact]
    public async Task ImportedTextRefreshesTheList()
    {
        var path = Path.Combine(_root, "script.txt");
        File.WriteAllText(path, "Alpha.\nBeta.\nGamma.");
        var window = CreateWindow();

        _wpf.Invoke(() => window.FilePathBox.Text = path);
        var work = _wpf.Invoke(() =>
        {
            window.ImportTextButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            return window.PendingWork;
        });
        await work;

        _wpf.Invoke(() => Assert.Equal(3, window.LineList.Items.Count));
    }

    [Fact]
    public void TheTextScrollsToKeepTheSpokenLineInTheMiddle()
    {
        var window = CreateWindow();

        _wpf.Invoke(() =>
        {
            window.Height = 600;
            window.Show();

            var box = window.InputText;
            box.Text = string.Join("\r\n", Enumerable.Range(1, 200).Select(n => $"Line {n}."));
            box.CaretIndex = 0;
            window.RebuildLineList();
            window.UpdateLayout();

            window.ActivePlayback = new FakePlayback();
            window.HighlightSpokenLine(box.GetCharacterIndexFromLineIndex(150));
            window.UpdateLayout();

            var first = box.GetFirstVisibleLineIndex();
            var last = box.GetLastVisibleLineIndex();
            Assert.InRange(150, first + 1, last - 1);
            Assert.InRange(150 - first, (last - first) / 2 - 2, (last - first) / 2 + 2);
            Assert.Equal(0, box.CaretIndex);

            window.ActivePlayback = null;
            window.Close();
        });
    }

    [Fact]
    public void DoubleClickingTheScrollBarReadsNothing()
    {
        var window = CreateWindow();

        _wpf.Invoke(() =>
        {
            window.Show();
            window.InputText.Text = string.Join("\n", Enumerable.Range(1, 300).Select(n => $"Line {n}."));
            window.RebuildLineList();
            window.LineList.SelectedIndex = 1;
            window.UpdateLayout();

            var scrollBar = Descendants(window.LineList).OfType<ScrollBar>()
                .First(bar => bar.Orientation == Orientation.Vertical);
            DoubleClick(window.LineList, scrollBar);
            Assert.NotEqual(8, window.LastReadStartOffset);

            var row = (ListBoxItem)window.LineList.ItemContainerGenerator.ContainerFromIndex(1);
            DoubleClick(window.LineList, row);
            Assert.Equal(8, window.LastReadStartOffset);

            window.Close();
        });
    }

    [Fact]
    public void ALongLineWrapsInsideItsOwnRow()
    {
        var window = CreateWindow();

        _wpf.Invoke(() =>
        {
            window.Show();
            var words = string.Join(" ", Enumerable.Repeat("words that keep going", 20));
            window.InputText.Text = $"Short.\n{words}\nShort again.";
            window.RebuildLineList();
            window.UpdateLayout();

            var rows = Enumerable.Range(0, 3)
                .Select(i => (ListBoxItem)window.LineList.ItemContainerGenerator.ContainerFromIndex(i))
                .ToList();
            Assert.True(rows[1].ActualHeight > rows[0].ActualHeight * 2);
            Assert.True(rows[1].ActualWidth <= window.LineList.ActualWidth);

            var top = rows[1].TranslatePoint(new Point(0, 0), window.LineList).Y;
            var next = rows[2].TranslatePoint(new Point(0, 0), window.LineList).Y;
            Assert.Equal(top + rows[1].ActualHeight, next, 1);
            Assert.Equal("2.  " + words, System.Windows.Automation.AutomationProperties.GetName(rows[1]));

            window.Close();
        });
    }

    private static void DoubleClick(Control target, object source) =>
        target.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = Control.MouseDoubleClickEvent,
            Source = source,
        });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var deeper in Descendants(child)) yield return deeper;
        }
    }

    private void Click(ButtonBase button) =>
        _wpf.Invoke(() => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));

    private MainWindow CreateWindow()
    {
        var window = _wpf.Invoke(() =>
        {
            var settings = AppSettings.LoadFrom(_settingsPath);
            settings.VoicesDirectory = _voicesDir;
            settings.UseWindowsVoices = false;
            settings.OutputDirectory = _root;

            return new MainWindow(settings, loadVoiceOnSelection: false);
        });

        _windows.Add(window);
        return window;
    }

    private sealed class FakePlayback : IAudioPlayback
    {
        public bool IsPaused => false;

        public long PlayedCharacters => 0;

        public void Pause()
        {
        }

        public void Resume()
        {
        }

        public void Stop()
        {
        }

        public void WaitUntilDrained()
        {
        }

        public void WriteSamples(ReadOnlySpan<float> samples)
        {
        }

        public void WriteSilence(int milliseconds)
        {
        }

        public void Dispose()
        {
        }
    }
}
