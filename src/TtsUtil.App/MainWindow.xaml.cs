/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using TtsUtil.Core;
using TtsUtil.Core.Audio;
using TtsUtil.Core.Settings;
using TtsUtil.Core.Text;
using TtsUtil.Core.Tts;

namespace TtsUtil.App;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly bool _loadVoiceOnSelection;

    private IReadOnlyList<VoiceDescriptor> _voices = Array.Empty<VoiceDescriptor>();
    private ITtsEngine? _engine;
    private string? _loadedVoiceName;
    private CancellationTokenSource? _cancellation;
    private CancellationTokenSource? _typingCancellation;
    private IAudioPlayback? _player;
    private readonly SemaphoreSlim _typingTurnstile = new(1, 1);
    private int _queuedSnippets;
    private bool _initialising = true;
    private bool _busy;
    private readonly ObservableCollection<VoiceCatalogueRow> _catalogueRows = new();
    private CancellationTokenSource? _installCancellation;
    private bool _installing;
    private LineMap _lineMap = LineMap.Build(string.Empty);
    private DispatcherTimer? _lineRebuildTimer;
    private long _runStartOffset;
    private DispatcherTimer? _playbackFollow;
    private Action? _rerun;
    private bool _restarting;
    private int _spokenLine = -1;
    private bool _readingFromText;
    private int? _stoppedAt;
    private TextDocument? _runDocument;
    private IReadOnlyList<SpeakerInfo> _speakers = Array.Empty<SpeakerInfo>();
    private string? _speakerVoiceName;
    private int _speakerId;
    private bool _favouritesOnly;
    private IReadOnlyList<VoiceDescriptor> _shownVoices = Array.Empty<VoiceDescriptor>();
    private VoiceDescriptor? _selectedVoice;
    private bool _favouriteVoicesOnly;

    public MainWindow() : this(LoadSettingsWithOverrides())
    {
        // Only the real startup path looks for a release. Tests build the window directly.
        Loaded += (_, _) => StartUpdateCheck();
    }

    private static AppSettings LoadSettingsWithOverrides()
    {
        var settings = AppSettings.Load();
        var overridden = App.VoicesDirectoryOverride;
        if (!string.IsNullOrWhiteSpace(overridden)) settings.VoicesDirectory = overridden;
        return settings;
    }

    internal MainWindow(AppSettings settings, bool loadVoiceOnSelection = true)
    {
        WheelScrolling.Enable();
        _settings = settings;
        _loadVoiceOnSelection = loadVoiceOnSelection;
        Draft = TextDraft.Beside(settings.SourcePath);
        SpeakSnippet = snippet => _ = SpeakSnippetAsync(snippet);
        BatchFolderPicker = () => AskForDirectory(_settings.ResolvedOutputDirectory);
        AudioPathPicker = AskForAudioPath;
        BatchFilePicker = AskForFilesToTakeIn;
        UnsavedCloseAsker = AskAboutUnsavedClose;
        EntryPlayer = PlayEntryAsync;
        InitializeComponent();
        NewDocument();
        LoadSettingsIntoUi();
        _initialising = false;
        RefreshVoices();
        RefreshVoiceCatalogue();
        RebuildLineList();
        RefreshScripts();
        RefreshQueue();
        ShowRepeatMode();
        RestoreDraft();
        LoadSettingsMirror();
    }

    /// <summary>Where the Text tab is kept between sittings, beside the settings it belongs to.</summary>
    internal TextDraft Draft { get; }

    /// <summary>Puts back the documents the window closed on, so nothing typed is lost.</summary>
    private void RestoreDraft()
    {
        var documents = Draft.Read();
        if (documents.Count == 0) return;

        _documents.Clear();
        DocumentTabs.Items.Clear();
        _plusTab = null;

        foreach (var document in documents) NewDocument(document.Title, document.Text, document.ScriptTitle);

        DocumentTabs.SelectedIndex = 0;
        InputText.CaretIndex = InputText.Text.Length;
        RebuildLineList();
    }

    /// <summary>Keeps the Text tab for next time, on the way out.</summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_settings.CloseToTray && !_quitting)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        foreach (var job in _writeJobs.ToList()) job.Cancellation.Cancel();

        Draft.Write(_documents
            .Select(document => new DraftDocument
            {
                Title = document.Title,
                ScriptTitle = document.ScriptTitle,
                Text = document.Box.Text,
            })
            .ToList());

        _settings.Save();
        _tray?.Dispose();
        base.OnClosing(e);
    }

    /// <summary>Where saved scripts live. Tests point it at a temporary folder.</summary>
    internal ScriptLibrary Scripts { get; set; } = new(ScriptLibrary.ResolveDirectory(
        AppSettings.AppDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        File.Exists));

    /// <summary>Opens a folder in Explorer. Tests replace it so nothing is launched.</summary>
    internal Action<string> FolderOpener { get; set; } = folder =>
        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });

    /// <summary>Builds the PDF reader. Tests replace it so no OCR engine is needed.</summary>
    internal Func<PdfTextExtractor> PdfExtractorFactory { get; set; } =
        () => new PdfTextExtractor(WindowsPageOcr.IsAvailable ? new WindowsPageOcr() : null);

    /// <summary>Builds the installer used by the Voices tab. Tests replace it with a fake.</summary>
    internal Func<VoiceInstaller> VoiceInstallerFactory { get; set; } =
        () => new VoiceInstaller(new HttpVoiceDownloader(), new TarArchiveExtractor());

    /// <summary>Speaks one completed word during playback on input mode.</summary>
    internal Action<string> SpeakSnippet { get; set; }

    /// <summary>Reports an error as (message, title). Defaults to a modal dialog.</summary>
    internal Action<string, string> ErrorReporter { get; set; } = (message, title) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    /// <summary>Asks a yes or no question. Tests answer it without a dialog.</summary>
    internal Func<string, string, bool> Confirmer { get; set; } = (message, title) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    private bool Confirm(string message, string title) => Confirmer(message, title);

    /// <summary>Asks whether a tab with unsaved words should go, and whether to stop asking.
    /// Tests answer it without a dialog.</summary>
    internal Func<string, (bool Close, bool StopAsking)> UnsavedCloseAsker { get; set; }

    internal AppSettings Settings => _settings;

    internal IReadOnlyList<VoiceDescriptor> Voices => _voices;

    /// <summary>The voice a run uses, which stays put when the picker is filtered down to the
    /// starred voices and this one is not among them.</summary>
    private VoiceDescriptor? SelectedVoice => _selectedVoice;

    /// <summary>The voices the picker is showing, the starred ones first.</summary>
    internal IReadOnlyList<VoiceDescriptor> ShownVoices => _shownVoices;

    private void LoadSettingsIntoUi()
    {
        SilenceLineEndingBox.Text = _settings.SilenceLineEndingMs.ToString();
        SilenceSentenceBox.Text = _settings.SilenceSentenceMs.ToString();
        SilenceQuestionBox.Text = _settings.SilenceQuestionMs.ToString();
        SilenceExclamationBox.Text = _settings.SilenceExclamationMs.ToString();
        ScaleSilenceBox.IsChecked = _settings.ScaleSilenceToRate;
        FilterHashBox.IsChecked = _settings.FilterHashes;
        FilterWebBox.IsChecked = _settings.FilterWebLinks;
        FilterMailBox.IsChecked = _settings.FilterMailToLinks;
        PopulateSpokenCharacterChoices();
        PopulateOutputFormatChoices();
        LoadEditorToolbar();
        LoadAliases();
        LoadHistorySettings();
        Mp3BitRateBox.Text = (_settings.Mp3BitRate / 1000).ToString();
        AllowedExtraBox.Text = _settings.AllowedExtraCharacters;
        VoicesDirBox.Text = _settings.ResolvedVoicesDirectory;
        OutputDirBox.Text = _settings.ResolvedOutputDirectory;
        ThreadsBox.Text = _settings.NumThreads?.ToString() ?? string.Empty;
        ChunkLengthBox.Text = _settings.MaxChunkLength.ToString();
        ReadAsYouTypeBox.IsChecked = _settings.ReadAsYouType;
        PauseWhenUnfocusedBox.IsChecked = _settings.PauseWhenUnfocused;
        CheckForUpdatesBox.IsChecked = _settings.CheckForUpdates;
        PromptForUpdatesBox.IsChecked = _settings.PromptForUpdates;
        ShowUpdateState();
        UseWindowsVoicesBox.IsChecked = _settings.UseWindowsVoices;
        SaveScriptWithAudioBox.IsChecked = _settings.SaveScriptWithAudio;
        WarnOnClosingUnsavedBox.IsChecked = _settings.WarnOnClosingUnsaved;
        CloseToTrayBox.IsChecked = _settings.CloseToTray;
        SpeedSlider.Value = Math.Clamp(_settings.Speed, 0.5, 2.0);
        SpeedText.Text = $"{_settings.Speed:0.00}x";
        SettingsPathText.Text = $"Settings file: {AppSettings.SettingsPath}";
        VersionText.Text = $"Version {AppVersion.Display}";
    }

    private void ApplySettingsFromUi()
    {
        _settings.SilenceLineEndingMs = ParseInt(SilenceLineEndingBox.Text, _settings.SilenceLineEndingMs);
        _settings.SilenceSentenceMs = ParseInt(SilenceSentenceBox.Text, _settings.SilenceSentenceMs);
        _settings.SilenceQuestionMs = ParseInt(SilenceQuestionBox.Text, _settings.SilenceQuestionMs);
        _settings.SilenceExclamationMs = ParseInt(SilenceExclamationBox.Text, _settings.SilenceExclamationMs);
        _settings.ScaleSilenceToRate = ScaleSilenceBox.IsChecked == true;
        _settings.FilterHashes = FilterHashBox.IsChecked == true;
        _settings.FilterWebLinks = FilterWebBox.IsChecked == true;
        _settings.FilterMailToLinks = FilterMailBox.IsChecked == true;
        _settings.SpokenCharacters = SelectedSpokenCharacterPolicy();
        _settings.OutputFormat = OutputFormatBox.SelectedIndex == 1 ? AudioOutputFormat.Wav : AudioOutputFormat.Mp3;
        _settings.Mp3BitRate = Math.Clamp(ParseInt(Mp3BitRateBox.Text, 128), 32, 320) * 1000;
        _settings.AllowedExtraCharacters = AllowedExtraBox.Text ?? string.Empty;
        _settings.NumThreads = ParseOptionalInt(ThreadsBox.Text) is int threads
            ? Math.Clamp(threads, ThreadPlan.Minimum, ThreadPlan.Maximum)
            : null;
        _settings.MaxChunkLength = Math.Clamp(ParseInt(ChunkLengthBox.Text, _settings.MaxChunkLength), 64, 20000);
        _settings.ReadAsYouType = ReadAsYouTypeBox.IsChecked == true;
        _settings.CheckForUpdates = CheckForUpdatesBox.IsChecked == true;
        _settings.SaveScriptWithAudio = SaveScriptWithAudioBox.IsChecked == true;
        _settings.WarnOnClosingUnsaved = WarnOnClosingUnsavedBox.IsChecked == true;
        _settings.CloseToTray = CloseToTrayBox.IsChecked == true;
        _settings.Speed = ReadSpeed();

        var outputDir = OutputDirBox.Text.Trim();
        _settings.OutputDirectory = outputDir.Length == 0 ? null : outputDir;

        var voicesDir = VoicesDirBox.Text.Trim();
        var voicesChanged = !string.Equals(voicesDir, _settings.ResolvedVoicesDirectory, StringComparison.OrdinalIgnoreCase);
        _settings.VoicesDirectory = voicesDir.Length == 0 ? null : voicesDir;
        ApplyHistorySettings();

        _settings.Save();
        LoadSettingsIntoUi();
        LoadSettingsMirror();
        if (voicesChanged) RefreshVoices();
    }

    /// <summary>The speed typed on the Settings tab, held to what the slider can reach.</summary>
    private float ReadSpeed()
    {
        if (!double.TryParse(SettingsSpeedBox.Text, out var speed)) return _settings.Speed;

        return (float)Math.Clamp(speed, 0.5, 2.0);
    }

    private void RefreshVoices()
    {
        var directory = _settings.ResolvedVoicesDirectory;
        var downloaded = VoiceCatalog.Scan(directory);
        var windows = _settings.UseWindowsVoices ? WindowsVoiceScanner() : Array.Empty<VoiceDescriptor>();
        _voices = downloaded.Concat(windows).ToList();

        AboutVoicesText.Text = $"Voices directory: {directory}\n{downloaded.Count} downloaded, "
            + $"{windows.Count} from Windows.";

        if (_voices.Count == 0)
        {
            _favouriteVoicesOnly = false;
            ShowFavouriteVoicesButton.Content = "Favorites";
            ShowVoices(null);
            _selectedVoice = null;
            SetStatus($"No voices found in {directory}. Install one from the Voices tab.");
            SpeakerBox.Items.Clear();
            LicenseText.Text = string.Empty;
            return;
        }

        ShowVoices(_settings.LastVoiceName);
    }

    /// <summary>Fills the picker with the starred voices first, or with only those, keeping the
    /// named voice chosen while it is still on show.</summary>
    private void ShowVoices(string? keep)
    {
        var starred = _voices.Where(voice => _settings.IsFavouriteVoice(voice.Name)).ToList();
        _shownVoices = _favouriteVoicesOnly
            ? starred
            : starred.Concat(_voices.Where(voice => !_settings.IsFavouriteVoice(voice.Name))).ToList();

        _initialising = true;
        VoiceBox.Items.Clear();
        foreach (var voice in _shownVoices) VoiceBox.Items.Add(VoiceLabel(voice));
        _initialising = false;

        var index = IndexOfVoice(keep);
        if (index < 0 && !_favouriteVoicesOnly && _shownVoices.Count > 0) index = 0;

        VoiceBox.SelectedIndex = index;
        FavouriteVoiceButton.IsChecked = _settings.IsFavouriteVoice(_selectedVoice?.Name);

        // The filter can hide the voice that is still the one a run would use.
        if (index < 0 && _selectedVoice is not null) SetStatus($"Still using {_selectedVoice.Name}.");
    }

    private void OnToggleFavouriteVoice(object sender, RoutedEventArgs e)
    {
        var voice = SelectedVoice;
        if (voice is null) return;

        _settings.ToggleFavouriteVoice(voice.Name);
        _settings.Save();

        if (_favouriteVoicesOnly && !_voices.Any(one => _settings.IsFavouriteVoice(one.Name)))
        {
            _favouriteVoicesOnly = false;
            ShowFavouriteVoicesButton.Content = "Favorites";
        }

        ShowVoices(voice.Name);
    }

    private void OnShowFavouriteVoices(object sender, RoutedEventArgs e)
    {
        if (!_favouriteVoicesOnly && !_voices.Any(one => _settings.IsFavouriteVoice(one.Name)))
        {
            SetStatus("No starred voices yet. Pick one and press Star.");
            return;
        }

        _favouriteVoicesOnly = !_favouriteVoicesOnly;
        ShowFavouriteVoicesButton.Content = _favouriteVoicesOnly ? "Show all" : "Favorites";
        ShowVoices(SelectedVoice?.Name);
    }

    private async Task<ITtsEngine?> EnsureEngineAsync()
    {
        var voice = SelectedVoice;
        if (voice is null)
        {
            SetStatus("Select a voice first.");
            return null;
        }

        if (_engine is not null && _loadedVoiceName == voice.Name) return _engine;

        DisposeEngine();
        SetStatus($"Loading {voice.Name}...");

        try
        {
            var threads = _settings.ResolvedNumThreads;
            var engine = await Task.Run(() => LoadEngine(voice, threads));
            _engine = engine;
            _loadedVoiceName = voice.Name;
            PopulateSpeakers(voice, engine.SpeakerCount);
            SetStatus($"{voice.Name} loaded: {engine.SampleRate} Hz, {engine.SpeakerCount} speaker(s).");
            return engine;
        }
        catch (Exception ex)
        {
            SetStatus($"Could not load {voice.Name}: {ex.Message}");
            ErrorReporter(ex.Message, "Voice failed to load");
            return null;
        }
    }

    /// <summary>The policies the Settings tab offers, in the order they are shown.</summary>
    private static readonly (SpokenCharacterPolicy Policy, string Label)[] SpokenChoices =
    {
        (SpokenCharacterPolicy.LatinOnly, "Letters and digits only (a-z, A-Z, 0-9)"),
        (SpokenCharacterPolicy.AnyLetter, "Any letter or digit, including accents and other scripts"),
        (SpokenCharacterPolicy.Off, "Everything, including punctuation and symbols"),
    };

    private void PopulateOutputFormatChoices()
    {
        if (OutputFormatBox.Items.Count == 0)
        {
            OutputFormatBox.Items.Add("MP3, smaller and plays anywhere");
            OutputFormatBox.Items.Add("WAV, uncompressed");
        }

        OutputFormatBox.SelectedIndex = _settings.OutputFormat == AudioOutputFormat.Wav ? 1 : 0;
    }

    private void PopulateSpokenCharacterChoices()
    {
        if (SpokenCharactersBox.Items.Count == 0)
        {
            foreach (var choice in SpokenChoices) SpokenCharactersBox.Items.Add(choice.Label);
        }

        var index = Array.FindIndex(SpokenChoices, choice => choice.Policy == _settings.SpokenCharacters);
        SpokenCharactersBox.SelectedIndex = index < 0 ? 0 : index;
    }

    private SpokenCharacterPolicy SelectedSpokenCharacterPolicy()
    {
        var index = SpokenCharactersBox.SelectedIndex;
        return index >= 0 && index < SpokenChoices.Length
            ? SpokenChoices[index].Policy
            : SpokenCharacterPolicy.LatinOnly;
    }

    /// <summary>Reads the voice's speaker names, then fills the picker and restores the choice.</summary>
    internal void PopulateSpeakers(VoiceDescriptor? voice, int count)
    {
        _speakerVoiceName = voice?.Name;
        _speakers = voice is null
            ? SpeakerCatalog.Build(count)
            : SpeakerCatalog.Load(voice, count);

        _speakerId = Math.Clamp(_settings.GetSpeakerId(_speakerVoiceName), 0, Math.Max(0, count - 1));

        _favouritesOnly = false;
        ShowFavouritesButton.Content = "Favorites";
        SpeakerSearchBox.Text = string.Empty;
        RefreshSpeakerList();

        var visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed;
        SpeakerPanel.Visibility = visibility;
        SpeakerBox.Visibility = visibility;
        SpeakerLabel.Visibility = visibility;
    }

    /// <summary>Applies the search box and the starred speakers to what the picker shows.</summary>
    private void RefreshSpeakerList()
    {
        var favourites = _settings.GetFavouriteSpeakers(_speakerVoiceName);
        var pool = _favouritesOnly
            ? _speakers.Where(speaker => favourites.Contains(speaker.Id)).ToList()
            : _speakers;
        var shown = SpeakerCatalog.Filter(pool, SpeakerSearchBox.Text, favourites);

        _initialising = true;
        SpeakerBox.Items.Clear();
        foreach (var speaker in shown) SpeakerBox.Items.Add(speaker);

        // The selected speaker can be filtered out, and then nothing is selected to change.
        var index = shown.ToList().FindIndex(speaker => speaker.Id == _speakerId);
        SpeakerBox.SelectedIndex = index;
        _initialising = false;

        FavouriteSpeakerButton.IsChecked = _settings.IsFavouriteSpeaker(_speakerVoiceName, _speakerId);

        var count = shown.Count == _speakers.Count
            ? $"{_speakers.Count} speakers"
            : $"{shown.Count} of {_speakers.Count} speakers";

        // The filter can hide the speaker that is still the one a run would use.
        SpeakerCountText.Text = index >= 0 ? count : $"{count}, still using {ActiveSpeakerLabel()}";
    }

    /// <summary>The speaker the next run uses, whatever the picker is filtered down to.</summary>
    internal int SelectedSpeakerId => _speakerId;

    private string ActiveSpeakerLabel() =>
        _speakers.FirstOrDefault(speaker => speaker.Id == _speakerId)?.Label ?? _speakerId.ToString();

    private async Task<RunOutcome> RunSynthesisAsync(Func<TextReader> readerFactory, string? outputPath,
        long? knownCharacters = null, long startOffset = 0, bool startPaused = false)
    {
        if (_busy)
        {
            SetStatus("Already working. Press Stop first.");
            return RunOutcome.Failed;
        }

        var engine = await EnsureEngineAsync();
        if (engine is null) return RunOutcome.Failed;

        // Every run starts from the voice's loaded state, so a restart or a repeat sounds the same.
        if (engine.NeedsVoiceReset)
        {
            SetStatus("Preparing the voice...");
            await Task.Run(engine.ResetVoice);
        }

        CancelTypingPlayback();

        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        var token = cancellation.Token;

        var options = _settings.ToChunkerOptions();
        var speakerId = _speakerId;
        var speed = (float)SpeedSlider.Value;
        _runStartOffset = startOffset;
        _spokenLine = -1;
        ShowPauseState(startPaused);
        IProgress<string> encodeProgress = new Progress<string>(SetStatus);

        var progress = new Progress<SynthesisProgress>(p =>
        {
            Progress.Value = p.Percent;
            if (p.TotalCharacters > 0) SetStatus($"{p.Percent}% ({p.CharactersRead} of {p.TotalCharacters} characters)");

            // A reading follows the listener instead, which trails synthesis by seconds.
            if (_player is null) HighlightSpokenLine(p.CharactersRead);
        });

        SetBusy(true);
        if (outputPath is null) StartFollowingPlayback();

        try
        {
            var result = await Task.Run(() =>
            {
                // Counting beats a byte length, which overstates the total on non ASCII text.
                var totalCharacters = knownCharacters ?? TextMeasure.CountCharacters(readerFactory);
                using var reader = AliasTextReader.Wrap(readerFactory(), ActiveAliases());
                var runner = new SynthesisRunner(engine, options) { SpeakerId = speakerId, Speed = speed };

                if (outputPath is null)
                {
                    using var player = PlayerFactory(engine.SampleRate, token);
                    _player = player;
                    if (startPaused) player.Pause();
                    try
                    {
                        runner.Run(reader, totalCharacters, player, progress, token);
                        player.WaitUntilDrained();
                    }
                    finally
                    {
                        _player = null;
                    }
                }
                else
                {
                    // Disposing encodes when the sink is an MP3 one, on this worker thread.
                    var sink = CreateFileSink(outputPath, engine.SampleRate, encodeProgress.Report);
                    try
                    {
                        runner.Run(reader, totalCharacters, sink, progress, token);
                    }
                    finally
                    {
                        (sink as IDisposable)?.Dispose();
                    }
                }

                return (runner.CharactersFiltered, runner.UtterancesSpoken, totalCharacters);
            }, token);

            var (filtered, spoken, total) = result;

            // Everything filtered away is silent failure, and the usual cause is the policy.
            if (spoken == 0 && total > 0)
            {
                SetStatus("Nothing was left to read: every character was filtered. " +
                          "Check \"Characters read aloud\" in Settings.");
                return RunOutcome.Failed;
            }

            var note = filtered > 0 ? $" {filtered} character(s) filtered." : string.Empty;

            if (outputPath is null)
            {
                SetStatus($"Finished reading.{note}");
            }
            else
            {
                SetStatusWithFileLink($"Wrote {Path.GetFileName(outputPath)}.{note} ", outputPath);
                NotifyWriteFinished(outputPath);
            }

            return RunOutcome.Finished;
        }
        catch (OperationCanceledException)
        {
            SetStatus("Stopped.");
            return RunOutcome.Stopped;
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
            ErrorReporter(ex.Message, "Synthesis failed");
            return RunOutcome.Failed;
        }
        finally
        {
            StopFollowingPlayback();
            SetBusy(false);
            Progress.Value = 0;
            _cancellation = null;
            cancellation.Dispose();
        }
    }

    private void OnReadText(object sender, RoutedEventArgs e)
    {
        if (_busy && _rerun is not null)
        {
            _ = RestartAsync(_rerun);
            return;
        }

        ReadFrom(0);
    }

    private void OnReadFromHere(object sender, RoutedEventArgs e) => ReadOrRestartFrom(ChosenStartOffset());

    private void OnLineListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LineList.SelectedItem is not ScriptLineRow row) return;
        ReadOrRestartFrom(row.Start);
    }

    /// <summary>Picking a line while reading moves the reading there rather than refusing.</summary>
    private void ReadOrRestartFrom(int offset)
    {
        if (_busy && _rerun is not null)
        {
            _ = RestartAsync(() => ReadFrom(offset));
            return;
        }

        ReadFrom(offset);
    }

    /// <summary>Stops the run in flight and starts it again once it has unwound.</summary>
    internal async Task RestartAsync(Action start)
    {
        if (_restarting) return;

        _restarting = true;
        try
        {
            StopRun("Restarting...");
            await CurrentRun;
            start();
        }
        finally
        {
            _restarting = false;
        }
    }

    /// <summary>The line the user picked in the list, or failing that the caret's line.</summary>
    private int ChosenStartOffset()
    {
        if (LineList.SelectedItem is ScriptLineRow row) return row.Start;

        RebuildLineList();
        return _lineMap.StartOf(_lineMap.LineAt(InputText.CaretIndex));
    }

    private void ReadFrom(int startOffset, bool startPaused = false)
    {
        var text = InputText.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("There is no text to read.");
            return;
        }

        var offset = Math.Clamp(startOffset, 0, text.Length);
        var remainder = text[offset..];

        if (string.IsNullOrWhiteSpace(remainder))
        {
            SetStatus("There is nothing left to read from there.");
            return;
        }

        LastReadStartOffset = offset;
        RememberStoppedSpot(null);
        _readingFromText = true;
        _runDocument = ActiveDocument;
        _rerun = () => ReadFrom(offset);
        CurrentRun = ReadRepeatedlyAsync(remainder, offset, startPaused);
    }

    /// <summary>Reads the same text again while repeat is on, until something stops it.</summary>
    private async Task ReadRepeatedlyAsync(string text, long offset, bool startPaused = false)
    {
        do
        {
            var outcome = await RunSynthesisAsync(() => new StringReader(text), null, text.Length, offset, startPaused);
            if (outcome != RunOutcome.Finished) return;

            // Only the reading that was interrupted waits. A repeat of it plays straight through.
            startPaused = false;
        }
        while (_settings.Repeat != RepeatMode.Off);
    }

    /// <summary>Where the last Read started, which is what "read from here" actually decides.</summary>
    internal int LastReadStartOffset { get; private set; } = -1;

    // --- The queue ---

    private readonly List<QueueEntry> _queue = new();

    /// <summary>What is waiting to be read, in the order it will be read.</summary>
    internal IReadOnlyList<QueueEntry> Queue => _queue;

    /// <summary>Reads one entry. Replaced in tests, which have no voice to read with.</summary>
    internal Func<QueueEntry, Task<RunOutcome>> EntryPlayer { get; set; }

    /// <summary>Plays from one entry on, obeying repeat, until it is stopped or runs out.</summary>
    internal async Task PlaySequenceAsync(IReadOnlyList<QueueEntry> items, int startIndex)
    {
        if (items.Count == 0)
        {
            SetStatus("The queue is empty. Add a script to it first.");
            return;
        }

        var index = Math.Clamp(startIndex, 0, items.Count - 1);
        var readThisPass = 0;

        while (true)
        {
            if (ReferenceEquals(items, _queue)) ShowQueuePosition(index);

            var outcome = await EntryPlayer(items[index]);
            if (outcome == RunOutcome.Stopped) return;
            if (outcome == RunOutcome.Finished) readThisPass++;

            var repeat = _settings.Repeat;

            if (repeat == RepeatMode.One)
            {
                // An entry that cannot be read would otherwise spin here for ever.
                if (outcome != RunOutcome.Finished) return;
                continue;
            }

            var next = index + 1;

            if (next >= items.Count)
            {
                if (repeat != RepeatMode.All || readThisPass == 0) return;

                next = 0;
                readThisPass = 0;
            }

            index = next;
        }
    }

    private async Task<RunOutcome> PlayEntryAsync(QueueEntry entry)
    {
        if (entry.ScriptTitle is string script) ApplyScriptVoice(script);

        var text = TextFor(entry);
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus($"{entry.Title} has nothing to read.");
            return RunOutcome.Failed;
        }

        SetStatus($"Reading {entry.Title}.");
        return await RunSynthesisAsync(() => new StringReader(text), null, text.Length);
    }

    private string TextFor(QueueEntry entry)
    {
        if (entry.ScriptTitle is null) return InputText.Text;

        try
        {
            return Scripts.Load(entry.ScriptTitle);
        }
        catch (IOException ex)
        {
            SetStatus($"Could not open {entry.Title}: {ex.Message}");
            return string.Empty;
        }
    }

    private void OnAddToQueue(object sender, RoutedEventArgs e)
    {
        var entry = SelectedScript is SavedScript script
            ? QueueEntry.ForScript(script.Title)
            : QueueEntry.ForTextTab();

        _queue.Add(entry);
        RefreshQueue();
        SetStatus($"Added {entry.Title} to the queue.");
    }

    private void OnRemoveFromQueue(object sender, RoutedEventArgs e)
    {
        var index = QueueList.SelectedIndex;
        if (index < 0) return;

        _queue.RemoveAt(index);
        RefreshQueue();
        QueueList.SelectedIndex = Math.Min(index, _queue.Count - 1);
    }

    private void OnMoveQueueItemUp(object sender, RoutedEventArgs e) => MoveQueueItem(-1);

    private void OnMoveQueueItemDown(object sender, RoutedEventArgs e) => MoveQueueItem(1);

    private void MoveQueueItem(int by)
    {
        var from = QueueList.SelectedIndex;
        var to = from + by;
        if (from < 0 || to < 0 || to >= _queue.Count) return;

        (_queue[from], _queue[to]) = (_queue[to], _queue[from]);
        RefreshQueue();
        QueueList.SelectedIndex = to;
    }

    private void OnClearQueue(object sender, RoutedEventArgs e)
    {
        _queue.Clear();
        RefreshQueue();
    }

    private void OnPlayQueue(object sender, RoutedEventArgs e)
    {
        if (_busy && _rerun is not null)
        {
            _ = RestartAsync(_rerun);
            return;
        }

        StartQueue(Math.Max(QueueList.SelectedIndex, 0));
    }

    private void OnQueueDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (QueueList.SelectedIndex < 0) return;

        if (_busy)
        {
            var from = QueueList.SelectedIndex;
            _ = RestartAsync(() => StartQueue(from));
            return;
        }

        StartQueue(QueueList.SelectedIndex);
    }

    private void StartQueue(int index)
    {
        _rerun = () => StartQueue(index);
        CurrentRun = PlaySequenceAsync(_queue, index);
    }

    internal void RefreshQueue()
    {
        QueueList.Items.Clear();
        foreach (var entry in _queue) QueueList.Items.Add(entry);
        QueueCountText.Text = _queue.Count == 0 ? "Nothing queued" : $"{_queue.Count} queued";
    }

    private void ShowQueuePosition(int index)
    {
        if (index < QueueList.Items.Count) QueueList.SelectedIndex = index;
    }

    private void OnCycleRepeat(object sender, RoutedEventArgs e)
    {
        _settings.Repeat = _settings.Repeat switch
        {
            RepeatMode.Off => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            _ => RepeatMode.Off,
        };

        _settings.Save();
        ShowRepeatMode();
    }

    /// <summary>Glyphs are from Segoe MDL2 Assets, which every supported Windows carries.</summary>
    private void ShowRepeatMode()
    {
        var (glyph, label) = _settings.Repeat switch
        {
            RepeatMode.One => ("", "Repeat one"),
            RepeatMode.All => ("", "Repeat all"),
            _ => ("", "Repeat off"),
        };

        // The same mode is shown twice, on the queue and beside the media buttons on the Text tab.
        foreach (var button in new[] { RepeatModeButton, TextRepeatButton })
        {
            button.Content = glyph;
            button.Opacity = _settings.Repeat == RepeatMode.Off ? 0.45 : 1.0;
            System.Windows.Automation.AutomationProperties.SetName(button, label);
        }

        RepeatModeText.Text = label;
        if (SettingsRepeatBox is not null) SettingsRepeatBox.SelectedIndex = Array.IndexOf(RepeatModes, _settings.Repeat);
    }

    // --- The line list ---

    /// <summary>Rebuilds the list from the editor, keeping the selected line where it can.</summary>
    internal void RebuildLineList()
    {
        _lineMap = LineMap.Build(InputText.Text);

        if (LinePanel.Visibility != Visibility.Visible)
        {
            LineList.Items.Clear();
            return;
        }

        var selected = (LineList.SelectedItem as ScriptLineRow)?.Index ?? -1;

        LineList.Items.Clear();
        foreach (var line in _lineMap.Lines) LineList.Items.Add(new ScriptLineRow(line));

        if (selected >= 0 && selected < LineList.Items.Count) LineList.SelectedIndex = selected;
    }

    /// <summary>Waits for typing to settle, so a long script is not rebuilt on every key.</summary>
    private void ScheduleLineRebuild()
    {
        _lineRebuildTimer ??= CreateLineRebuildTimer();
        _lineRebuildTimer.Stop();
        _lineRebuildTimer.Start();
    }

    private DispatcherTimer CreateLineRebuildTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            RebuildLineList();
        };
        return timer;
    }

    private void OnShowLinesChanged(object sender, RoutedEventArgs e)
    {
        // IsChecked in XAML fires this while the tree is still being built.
        if (LinePanel is null || LineSplitter is null || LineListColumn is null) return;

        var show = ShowLinesBox.IsChecked == true;
        LinePanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        LineSplitter.Visibility = LinePanel.Visibility;
        LineListColumn.Width = show ? new GridLength(300) : new GridLength(0);
        RebuildLineList();
    }

    /// <summary>Keeps the list on the line being heard, which lags what has been synthesized.</summary>
    private void StartFollowingPlayback()
    {
        if (_playbackFollow is null)
        {
            _playbackFollow = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(120),
            };

            _playbackFollow.Tick += (_, _) =>
            {
                var player = _player;
                if (player is not null) HighlightSpokenLine(player.PlayedCharacters);
            };
        }

        _playbackFollow.Start();
    }

    private void StopFollowingPlayback() => _playbackFollow?.Stop();

    /// <summary>Follows the run down the list, so the spoken line is visible.</summary>
    private void HighlightSpokenLine(long charactersRead)
    {
        if (LinePanel.Visibility != Visibility.Visible || LineList.Items.Count == 0) return;

        var line = _lineMap.LineAt(_runStartOffset + charactersRead);
        if (line == _spokenLine || line >= LineList.Items.Count) return;

        _spokenLine = line;
        LineList.SelectedIndex = line;
        LineList.ScrollIntoView(LineList.Items[line]);
    }

    private void OnSaveTextToWave(object sender, RoutedEventArgs e) => SaveTextToWave();

    private void OnSaveAudioCommand(object sender, ExecutedRoutedEventArgs e) => SaveTextToWave();

    /// <summary>Writes the open document to audio in the background, so other tabs and the
    /// reading are left alone.</summary>
    private void SaveTextToWave()
    {
        var document = ActiveDocument;
        var text = InputText.Text;
        if (document is null || string.IsNullOrWhiteSpace(text))
        {
            SetStatus("There is no text to write.");
            return;
        }

        var path = AudioPathPicker(AudioStem(ActiveScriptTitle));
        if (path is null)
        {
            SetStatus("No file was chosen, so nothing was written.");
            return;
        }

        KeepTextAsScript(text, path);
        PendingWork = WriteInBackgroundAsync(document, text, path);
    }

    /// <summary>Whether the run in flight is reading the text box. Tests set up a reading with it.</summary>
    internal bool ReadingFromText
    {
        get => _readingFromText;
        set => _readingFromText = value;
    }

    /// <summary>Writing audio needs the voice to itself, so a reading in flight gives way and is
    /// set up again where the listener had got to, paused.</summary>
    internal async Task SaveThenResumeAsync(Func<Task> save)
    {
        var resumeAt = ReadingSpot();
        if (resumeAt is not null)
        {
            StopRun("Pausing the reading to write the file...");
            await CurrentRun;
        }

        await save();

        if (resumeAt is int offset) ReadFrom(offset, startPaused: true);
    }

    /// <summary>The start of the line being heard, or null when no text is being read aloud.</summary>
    private int? ReadingSpot()
    {
        var player = _player;
        if (!_readingFromText || player is null) return null;

        return _lineMap.StartOf(_lineMap.LineAt(_runStartOffset + player.PlayedCharacters));
    }

    /// <summary>Stop keeps the line it cut off on, and the button only shows while there is one.</summary>
    private void RememberStoppedSpot(int? offset)
    {
        _stoppedAt = offset;
        ResumeButton.Visibility = offset is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Where a stopped reading would carry on from, or null when there is nothing to carry on.</summary>
    internal int? StoppedAt => _stoppedAt;

    private void OnResumeFromStop(object sender, RoutedEventArgs e)
    {
        if (_stoppedAt is not int offset)
        {
            SetStatus("There is no stopped reading to carry on from.");
            return;
        }

        ReadOrRestartFrom(offset);
    }

    private void OnReadFile(object sender, RoutedEventArgs e)
    {
        if (_busy && _rerun is not null)
        {
            _ = RestartAsync(_rerun);
            return;
        }

        CurrentRun = ReadOrConvertFileAsync(null);
    }

    private void OnSaveFileToWave(object sender, RoutedEventArgs e)
    {
        var path = FilePathBox.Text.Trim();
        if (!File.Exists(path))
        {
            SetStatus("Choose an existing file first.");
            return;
        }

        var outputPath = AudioPathPicker(Path.GetFileNameWithoutExtension(path));
        if (outputPath is null) return;

        PendingWork = SaveThenResumeAsync(() => CurrentRun = ReadOrConvertFileAsync(outputPath));
    }

    private async Task ReadOrConvertFileAsync(string? outputPath)
    {
        var path = FilePathBox.Text.Trim();
        if (!File.Exists(path))
        {
            SetStatus("Choose an existing file first.");
            return;
        }

        // Writing a file is not something Restart should turn into playback.
        _rerun = outputPath is null ? () => CurrentRun = ReadOrConvertFileAsync(null) : null;
        _readingFromText = false;

        if (!IsPdf(path))
        {
            await RunSynthesisAsync(() => OpenTextFile(path), outputPath);
            return;
        }

        var text = await ExtractPdfAsync(path);
        if (text is null) return;

        await RunSynthesisAsync(() => new StringReader(text), outputPath, text.Length);
    }

    private void OnImportFileToTextTab(object sender, RoutedEventArgs e) =>
        PendingWork = ImportFileToTextTabAsync();

    /// <summary>The last background task a click started, so tests can await it.</summary>
    internal Task PendingWork { get; private set; } = Task.CompletedTask;

    /// <summary>The reading in flight, which Restart waits on before starting the next one.</summary>
    internal Task CurrentRun { get; set; } = Task.CompletedTask;

    private async Task ImportFileToTextTabAsync()
    {
        var path = FilePathBox.Text.Trim();
        if (!File.Exists(path))
        {
            SetStatus("Choose an existing file first.");
            return;
        }

        string? text;
        if (IsPdf(path))
        {
            text = await ExtractPdfAsync(path);
            if (text is null) return;
        }
        else
        {
            using var reader = OpenTextFile(path);
            text = await reader.ReadToEndAsync();
        }

        InputText.Text = text;
        ActiveScriptTitle = null;
        RebuildLineList();
        Tabs.SelectedIndex = 0;
        SetStatus($"Imported {text.Length:N0} character(s) from {Path.GetFileName(path)}.");
    }

    /// <summary>Extracts a PDF on a worker thread, reporting pages. Null means it failed.</summary>
    private async Task<string?> ExtractPdfAsync(string path)
    {
        if (_busy)
        {
            SetStatus("Already working. Press Stop first.");
            return null;
        }

        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        var progress = new Progress<string>(SetStatus);

        SetBusy(true);

        try
        {
            var extractor = PdfExtractorFactory();
            var result = await Task.Run(
                () => extractor.ExtractAsync(path, progress, cancellation.Token), cancellation.Token);

            var text = result.Text;
            if (text.Trim().Length == 0)
            {
                SetStatus(result.ScannedPages.Count > 0
                    ? "This PDF holds no text layer and nothing could be read from its images. It may be a scan of a kind the Windows reader cannot handle."
                    : "This PDF holds no text.");
                return null;
            }

            FileInfoText.Text = DescribeExtraction(path, result);
            return text;
        }
        catch (OperationCanceledException)
        {
            _batchStopped = true;
            SetStatus("Stopped.");
            return null;
        }
        catch (Exception ex)
        {
            SetStatus($"Could not read {Path.GetFileName(path)}: {ex.Message}");
            ErrorReporter(ex.Message, "PDF could not be read");
            return null;
        }
        finally
        {
            SetBusy(false);
            _cancellation = null;
        }
    }

    private static string DescribeExtraction(string path, PdfExtractionResult result)
    {
        var name = Path.GetFileName(path);
        var note = $"{name}: {result.Pages.Count} page(s), {result.Text.Length:N0} characters.";

        if (result.ScannedPages.Count > 0)
        {
            note += $" {result.ScannedPages.Count} page(s) had no text layer and were read as images.";
        }

        if (result.EmptyPages.Count > 0)
        {
            note += $" Nothing was read from page(s): {string.Join(", ", result.EmptyPages)}.";
        }

        return note;
    }

    private static bool IsPdf(string path) =>
        PdfTextExtractor.HasPdfExtension(path) || PdfTextExtractor.LooksLikePdf(path);

    // --- Saved scripts ---

    /// <summary>The script the list is on, or null when the list is empty.</summary>
    private SavedScript? SelectedScript => (ScriptList.SelectedItem as ScriptRow)?.Script;

    /// <summary>Reloads the script list, keeping the selected title where it still exists.</summary>
    internal void RefreshScripts()
    {
        var selected = SelectedScript?.Title;
        var ticked = ChosenScripts().Select(s => s.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);

        ScriptList.Items.Clear();
        foreach (var script in Scripts.List())
        {
            var voice = Scripts.LoadProperties(script.Title)?.VoiceName;
            ScriptList.Items.Add(new ScriptRow(script, voice) { Chosen = ticked.Contains(script.Title) });
        }

        ScriptFolderText.Text = $"Scripts folder: {Scripts.Directory}";

        if (selected is null) return;

        var found = ScriptList.Items.Cast<ScriptRow>()
            .FirstOrDefault(s => string.Equals(s.Title, selected, StringComparison.OrdinalIgnoreCase));
        if (found is not null) ScriptList.SelectedItem = found;
    }

    private void OnScriptSelected(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedScript is not SavedScript script) return;

        ScriptTitleBox.Text = script.Title;

        try
        {
            ScriptPreview.Text = Scripts.Load(script.Title);
        }
        catch (IOException ex)
        {
            ScriptPreview.Text = string.Empty;
            SetStatus($"Could not read {script.Title}: {ex.Message}");
        }
    }

    private void OnSaveScript(object sender, RoutedEventArgs e) => SaveScriptFromText();

    /// <summary>Asks for the name, so a script is never quietly written over a stale title.
    /// Tests swap it out.</summary>
    internal Func<string, string?>? ScriptNamePrompt { get; set; }

    /// <summary>Ctrl+S and the Save button on the Text tab arrive here.</summary>
    internal void SaveScriptWithPrompt()
    {
        if (string.IsNullOrWhiteSpace(InputText.Text))
        {
            SetStatus("The Text tab is empty, so there is nothing to save.");
            return;
        }

        var suggested = ScriptTitleBox.Text.Trim();
        if (suggested.Length == 0) suggested = ActiveScriptTitle ?? ActiveDocument?.Title ?? string.Empty;

        var chosen = (ScriptNamePrompt ?? AskForScriptName)(suggested);
        if (chosen is null)
        {
            SetStatus("The script was not saved.");
            return;
        }

        ScriptTitleBox.Text = chosen;
        SaveScriptFromText();
    }

    private string? AskForScriptName(string suggested)
    {
        var dialog = new NameScriptWindow(suggested) { Owner = this };
        return dialog.ShowDialog() == true ? dialog.ChosenName : null;
    }

    /// <summary>Saves the Text tab under the title beside it. Ctrl+S arrives here too.</summary>
    internal void SaveScriptFromText()
    {
        var text = InputText.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("The Text tab is empty, so there is nothing to save.");
            return;
        }

        var title = ScriptLibrary.ToFileName(ScriptTitleBox.Text);
        if (title is null)
        {
            SetStatus("Give the script a title first.");
            return;
        }

        try
        {
            var existed = Scripts.Exists(title);
            var saved = Scripts.Save(title, text);
            KeepVoiceWithScript(saved.Title);
            ActiveScriptTitle = saved.Title;
            RefreshScripts();
            ScriptList.SelectedItem = ScriptList.Items.Cast<ScriptRow>()
                .FirstOrDefault(s => s.Title == saved.Title);

            MarkDocumentSaved(ActiveDocument);
            ShowSaveConfirmation(existed);
            SetStatus(existed ? $"Replaced {saved.Title}." : $"Saved {saved.Title}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus($"Could not save {title}: {ex.Message}");
            ErrorReporter(ex.Message, "Script could not be saved");
        }
    }

    private void OnOpenScript(object sender, RoutedEventArgs e)
    {
        if (SelectedScript is not SavedScript script)
        {
            SetStatus("Select a script first.");
            return;
        }

        try
        {
            InputText.Text = Scripts.Load(script.Title);
            ActiveScriptTitle = script.Title;
            MarkDocumentSaved(ActiveDocument);
            var voice = ApplyScriptVoice(script.Title);
            RebuildLineList();
            Tabs.SelectedIndex = 0;
            SetStatus(voice ? $"Opened {script.Title}, in the voice it was saved with." : $"Opened {script.Title}.");
        }
        catch (IOException ex)
        {
            SetStatus($"Could not open {script.Title}: {ex.Message}");
        }
    }

    private void OnScriptDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedScript is not SavedScript script) return;

        OpenScriptInNewTab(script);
    }

    /// <summary>Opens a script beside what is already there rather than over it, leaving the
    /// Scripts tab in front so another can be opened straight after.</summary>
    internal void OpenScriptInNewTab(SavedScript script)
    {
        try
        {
            var document = NewDocument(script.Title, Scripts.Load(script.Title), script.Title);
            DocumentTabs.SelectedItem = document.Tab;
            ApplyScriptVoice(script.Title);
            MarkNewTabs(1);
            SetStatus($"Opened {script.Title} in a new tab.");
        }
        catch (IOException ex)
        {
            SetStatus($"Could not open {script.Title}: {ex.Message}");
        }
    }

    private void OnRenameScript(object sender, RoutedEventArgs e)
    {
        if (SelectedScript is not SavedScript script)
        {
            SetStatus("Select a script first.");
            return;
        }

        var title = ScriptLibrary.ToFileName(ScriptTitleBox.Text);
        if (title is null)
        {
            SetStatus("Type the new title in the Title box first.");
            return;
        }

        if (!Scripts.Rename(script.Title, title))
        {
            SetStatus($"There is already a script called {title}.");
            return;
        }

        RefreshScripts();
        SetStatus($"Renamed to {title}.");
    }

    private void OnDeleteScript(object sender, RoutedEventArgs e)
    {
        if (SelectedScript is not SavedScript script)
        {
            SetStatus("Select a script first.");
            return;
        }

        if (!Confirm($"Delete the script \"{script.Title}\"? The file is removed from disk.", "Delete script"))
        {
            return;
        }

        try
        {
            Scripts.Delete(script.Title);
            ScriptPreview.Clear();
            RefreshScripts();
            SetStatus($"Deleted {script.Title}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus($"Could not delete {script.Title}: {ex.Message}");
        }
    }

    private void OnOpenScriptFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(Scripts.Directory);
            FolderOpener(Scripts.Directory);
        }
        catch (Exception ex)
        {
            SetStatus($"Could not open the scripts folder: {ex.Message}");
        }
    }

    private void OnStop(object sender, RoutedEventArgs e)
    {
        StopRun("Stopping...");
        if (_stoppedAt is not null) SetStatus("Stopped. Carry on picks the reading up at that line.");
    }

    private void StopRun(string message)
    {
        RememberStoppedSpot(ReadingSpot());
        CancelTypingPlayback();
        _cancellation?.Cancel();

        // Resuming first, so a run paused when Stop is pressed does not sit waiting to drain.
        _player?.Resume();
        _player?.Stop();
        ShowPauseState(paused: false);
        SetStatus(message);
    }

    private void OnTogglePause(object sender, RoutedEventArgs e)
    {
        var player = _player;
        if (player is null)
        {
            SetStatus("Nothing is playing.");
            return;
        }

        if (player.IsPaused)
        {
            player.Resume();
            ShowPauseState(paused: false);
            SetStatus("Playing.");
            return;
        }

        player.Pause();
        ShowPauseState(paused: true);
        SetStatus("Paused. Press Resume to carry on, or Stop to give up.");
    }

    /// <summary>Both tabs carry the button, so both follow the one playback state.</summary>
    private void ShowPauseState(bool paused)
    {
        var glyph = paused ? Glyph.Play : Glyph.Pause;
        var label = paused ? "Resume" : "Pause";
        ShowAction(PauseButton, glyph, label);
        ShowAction(PauseFileButton, glyph, label);

        if (paused || _busy) ShowMediaState(paused ? MediaState.Paused : MediaState.Playing);
    }

    /// <summary>Icon buttons show no words, so the wording lives in the tooltip and the accessible name.</summary>
    private static void ShowAction(System.Windows.Controls.Button button, string glyph, string label)
    {
        button.Content = glyph;
        button.ToolTip = label;
        System.Windows.Automation.AutomationProperties.SetName(button, label);
    }

    /// <summary>The playback a run is using, or null when nothing is playing.</summary>
    internal IAudioPlayback? ActivePlayback
    {
        get => _player;
        set => _player = value;
    }

    /// <summary>The player a run plays through. Tests replace it so no device is needed.</summary>
    internal Func<int, CancellationToken, IAudioPlayback> PlayerFactory { get; set; } =
        (sampleRate, token) => new AudioPlayerSink(sampleRate, token);

    private void OnPasteClipboard(object sender, RoutedEventArgs e)
    {
        if (!Clipboard.ContainsText())
        {
            SetStatus("The clipboard holds no text.");
            return;
        }

        ActiveScriptTitle = null;
        InputText.Text = Clipboard.GetText();
        InputText.CaretIndex = InputText.Text.Length;
    }

    private void OnClearText(object sender, RoutedEventArgs e)
    {
        ActiveScriptTitle = null;
        InputText.Clear();
    }

    private void OnBrowseInputFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a text file or a PDF",
            Filter = "Readable files (*.txt;*.md;*.csv;*.log;*.pdf)|*.txt;*.md;*.csv;*.log;*.pdf|" +
                     "PDF documents (*.pdf)|*.pdf|Text files (*.txt;*.md;*.csv;*.log)|*.txt;*.md;*.csv;*.log|" +
                     "All files (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) != true) return;

        FilePathBox.Text = dialog.FileName;
        var info = new FileInfo(dialog.FileName);
        FileInfoText.Text = $"{info.Name}: {info.Length:N0} bytes.";
    }

    private void OnBrowseVoicesDir(object sender, RoutedEventArgs e)
    {
        var chosen = AskForDirectory(VoicesDirBox.Text);
        if (chosen is not null) VoicesDirBox.Text = chosen;
    }

    private void OnBrowseOutputDir(object sender, RoutedEventArgs e)
    {
        var chosen = AskForDirectory(OutputDirBox.Text);
        if (chosen is not null) OutputDirBox.Text = chosen;
    }

    /// <summary>Shows the explanation for whichever question mark was pressed.</summary>
    private void OnSettingsHelp(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement source) return;

        var topic = SettingsHelp.Find(source.Tag as string);
        if (topic is null) return;

        SettingsHelpTitle.Text = topic.Title;
        SettingsHelpBody.Text = topic.Body;
        SettingsHelpPopup.PlacementTarget = source;
        SettingsHelpPopup.IsOpen = true;
    }

    private void OnApplySettings(object sender, RoutedEventArgs e)
    {
        ApplySettingsFromUi();
        SetStatus("Settings saved.");
    }

    private void OnRescanVoices(object sender, RoutedEventArgs e)
    {
        DisposeEngine();
        RefreshVoices();
    }

    private void RefreshVoiceCatalogue()
    {
        var directory = VoiceInstallDirectory;

        if (_catalogueRows.Count == 0)
        {
            foreach (var voice in DownloadableVoices.All) _catalogueRows.Add(new VoiceCatalogueRow(voice));
            VoiceCatalogueList.ItemsSource = _catalogueRows;
        }

        ShowVoicesAddedByHand(directory);

        foreach (var row in _catalogueRows)
        {
            row.Status = VoiceInstaller.IsInstalled(row.Voice, directory) ? "Installed" : "Not installed";
        }

        VoiceTargetText.Text = $"Installing into {directory}";
    }

    /// <summary>Lists the voices sitting in the folder that the built in list does not know about,
    /// and drops the rows for ones that have gone.</summary>
    private void ShowVoicesAddedByHand(string directory)
    {
        foreach (var gone in _catalogueRows
                     .Where(row => row.AddedByHand && !VoiceInstaller.IsInstalled(row.Voice, directory)).ToList())
        {
            _catalogueRows.Remove(gone);
        }

        var known = _catalogueRows.Select(row => row.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var found in VoiceCatalog.Scan(directory))
        {
            if (!known.Add(found.Name)) continue;

            _catalogueRows.Add(new VoiceCatalogueRow(new DownloadableVoice
            {
                Id = found.Name,
                Language = "Added by hand",
                Kind = found.Kind,
                Licence = "See the folder",
            })
            {
                AddedByHand = true,
            });
        }
    }

    /// <summary>Where a downloaded voice goes: the configured folder, or the profile when it is read only.</summary>
    internal string VoiceInstallDirectory => VoiceInstaller.ChooseTargetDirectory(
        _settings.ResolvedVoicesDirectory,
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            InstallPaths.ApplicationFolderName,
            InstallPaths.VoicesFolderName),
        VoiceInstaller.CanWrite);

    private VoiceCatalogueRow? SelectedCatalogueRow => VoiceCatalogueList.SelectedItem as VoiceCatalogueRow;

    /// <summary>The voices Install works through: every ticked one, or the selected row when
    /// nothing is ticked.</summary>
    internal List<VoiceCatalogueRow> VoiceInstallQueue()
    {
        var ticked = _catalogueRows.Where(row => row.Ticked).ToList();
        if (ticked.Count > 0) return ticked;

        var selected = SelectedCatalogueRow;
        return selected is null ? new List<VoiceCatalogueRow>() : new List<VoiceCatalogueRow> { selected };
    }

    private async void OnInstallVoice(object sender, RoutedEventArgs e)
    {
        if (_installing)
        {
            VoiceInstallStatus.Text = "Already installing. Press Cancel first.";
            return;
        }

        var queue = VoiceInstallQueue();

        if (queue.Count == 0)
        {
            VoiceInstallStatus.Text = "Select a voice to install.";
            return;
        }

        var directory = VoiceInstallDirectory;
        var waiting = queue.Where(row => !VoiceInstaller.IsInstalled(row.Voice, directory)).ToList();

        if (waiting.Count == 0)
        {
            VoiceInstallStatus.Text = queue.Count == 1
                ? $"{queue[0].Id} is already installed. Remove it first to download it again."
                : "Every ticked voice is already installed.";
            return;
        }

        _installing = true;
        _installCancellation = new CancellationTokenSource();
        InstallVoiceButton.IsEnabled = false;
        RemoveVoiceButton.IsEnabled = false;
        CancelVoiceButton.IsEnabled = true;
        foreach (var row in waiting) row.Status = "Queued";

        var done = 0;

        try
        {
            foreach (var row in waiting)
            {
                if (_installCancellation.Token.IsCancellationRequested) break;

                await InstallOneVoice(row, directory, done + 1, waiting.Count, _installCancellation.Token);
                done++;
            }
        }
        catch (OperationCanceledException)
        {
            VoiceProgress.Value = 0;
            VoiceInstallStatus.Text = waiting.Count == 1
                ? $"{waiting[0].Id} canceled; nothing was kept."
                : $"Canceled after {done} of {waiting.Count}; the rest were left alone.";
        }
        finally
        {
            _installCancellation?.Dispose();
            _installCancellation = null;
            _installing = false;
            LockPlaybackSettings(_busy);
            CancelVoiceButton.IsEnabled = false;
            RefreshVoiceCatalogue();
        }
    }

    private async Task InstallOneVoice(VoiceCatalogueRow row, string directory, int place, int of,
        CancellationToken cancellationToken)
    {
        var run = of == 1 ? string.Empty : $" ({place} of {of})";
        VoiceProgress.Value = 0;
        row.Status = "Installing";

        // Reports are posted, so late ones would overwrite the message the awaiting code leaves.
        var progress = new Progress<VoiceInstallProgress>(report =>
        {
            if (!_installing) return;

            switch (report.Phase)
            {
                case VoiceInstallPhase.Downloading:
                    VoiceProgress.Value = report.Percent;
                    VoiceInstallStatus.Text =
                        $"Downloading {row.Id}{run}: {report.Percent}% of {row.Voice.SizeMb} MB";
                    break;

                case VoiceInstallPhase.Extracting:
                    VoiceProgress.Value = 100;
                    VoiceInstallStatus.Text = $"Unpacking {row.Id}{run}...";
                    break;
            }
        });

        try
        {
            await VoiceInstallerFactory().InstallAsync(row.Voice, directory, progress, cancellationToken);

            VoiceProgress.Value = 100;
            VoiceInstallStatus.Text = $"{row.Id} installed{run}. License: {row.Licence}{ScriptWarning(row.Voice)}";
            row.Ticked = false;
            DisposeEngine();
            RefreshVoices();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            VoiceProgress.Value = 0;
            VoiceInstallStatus.Text = $"{row.Id} failed{run}: {ex.Message}";
            row.Status = "Not installed";
        }
    }

    /// <summary>The strict character setting would leave a non Latin voice with nothing to say.</summary>
    internal string ScriptWarning(DownloadableVoice voice)
    {
        if (!voice.NonLatinScript || _settings.SpokenCharacters != SpokenCharacterPolicy.LatinOnly)
        {
            return string.Empty;
        }

        return " This script needs \"Characters read aloud\" set to any letter or digit in Settings.";
    }

    private void OnRemoveVoice(object sender, RoutedEventArgs e)
    {
        var row = SelectedCatalogueRow;

        if (row is null)
        {
            VoiceInstallStatus.Text = "Select a voice to remove.";
            return;
        }

        var directory = VoiceInstallDirectory;

        if (!VoiceInstaller.IsInstalled(row.Voice, directory))
        {
            VoiceInstallStatus.Text = $"{row.Id} is not installed.";
            return;
        }

        if (string.Equals(_loadedVoiceName, row.Id, StringComparison.OrdinalIgnoreCase)) DisposeEngine();

        VoiceInstaller.Remove(row.Voice, directory);
        VoiceInstallStatus.Text = $"{row.Id} removed.";
        RefreshVoices();
        RefreshVoiceCatalogue();
    }

    private void OnCancelVoiceInstall(object sender, RoutedEventArgs e)
    {
        if (!_installing)
        {
            VoiceInstallStatus.Text = "Nothing is installing.";
            return;
        }

        _installCancellation?.Cancel();
        VoiceInstallStatus.Text = "Canceling...";
    }

    private async void OnVoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initialising) return;

        var voice = VoiceBox.SelectedIndex >= 0 && VoiceBox.SelectedIndex < _shownVoices.Count
            ? _shownVoices[VoiceBox.SelectedIndex]
            : null;
        if (voice is null || ReferenceEquals(voice, _selectedVoice)) return;

        _selectedVoice = voice;
        FavouriteVoiceButton.IsChecked = _settings.IsFavouriteVoice(voice.Name);
        _settings.LastVoiceName = voice.Name;
        _settings.Save();
        LicenseText.Text = ReadLicenseSummary(voice);

        CancelTypingPlayback();
        DisposeEngine();
        if (_loadVoiceOnSelection) await EnsureEngineAsync();
    }

    private void OnSpeakerChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initialising || SpeakerBox.SelectedItem is not SpeakerInfo speaker) return;

        _speakerId = speaker.Id;
        _settings.SetSpeakerId(_speakerVoiceName, speaker.Id);
        FavouriteSpeakerButton.IsChecked = _settings.IsFavouriteSpeaker(_speakerVoiceName, speaker.Id);
    }

    private void OnSpeakerSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (_speakers.Count == 0) return;
        RefreshSpeakerList();
    }

    private void OnToggleFavouriteSpeaker(object sender, RoutedEventArgs e)
    {
        if (_speakers.Count == 0) return;

        _settings.ToggleFavouriteSpeaker(_speakerVoiceName, _speakerId);
        _settings.Save();

        if (_favouritesOnly && _settings.GetFavouriteSpeakers(_speakerVoiceName).Count == 0)
        {
            _favouritesOnly = false;
            ShowFavouritesButton.Content = "Favorites";
        }

        RefreshSpeakerList();
    }

    private void OnShowFavouriteSpeakers(object sender, RoutedEventArgs e)
    {
        var favourites = _settings.GetFavouriteSpeakers(_speakerVoiceName);
        if (!_favouritesOnly && favourites.Count == 0)
        {
            SetStatus("No starred speakers for this voice yet. Pick one and press Star.");
            return;
        }

        _favouritesOnly = !_favouritesOnly;
        ShowFavouritesButton.Content = _favouritesOnly ? "Show all" : "Favorites";
        RefreshSpeakerList();
    }

    private void OnSpeedChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SpeedText is null) return;
        SpeedText.Text = $"{e.NewValue:0.00}x";
        _settings.Speed = (float)e.NewValue;
    }

    private void OnReadAsYouTypeChanged(object sender, RoutedEventArgs e)
    {
        if (_initialising) return;
        _settings.ReadAsYouType = ReadAsYouTypeBox.IsChecked == true;
        if (!_settings.ReadAsYouType) CancelTypingPlayback();
        SettingsReadAsYouTypeBox.IsChecked = ReadAsYouTypeBox.IsChecked;
    }

    private void OnPauseWhenUnfocusedChanged(object sender, RoutedEventArgs e)
    {
        if (_initialising) return;

        _settings.PauseWhenUnfocused = PauseWhenUnfocusedBox.IsChecked == true;
        _settings.Save();
        SettingsPauseWhenUnfocusedBox.IsChecked = PauseWhenUnfocusedBox.IsChecked;
    }

    private void OnWindowDeactivated(object sender, EventArgs e) => HoldForLostFocus();

    /// <summary>Another window taking over holds the reading, so nothing is missed while the
    /// listener is away. It carries on when Resume is pressed.</summary>
    internal void HoldForLostFocus()
    {
        if (!_settings.PauseWhenUnfocused) return;

        var player = _player;
        if (player is null || player.IsPaused) return;

        player.Pause();
        ShowPauseState(paused: true);
        SetStatus("Paused, with the window no longer in front. Press Resume to carry on.");
    }

    private void OnInputTextChanged(object sender, TextChangedEventArgs e)
    {
        ScheduleLineRebuild();
        if (DocumentFor(sender) is TextDocument edited) ShowTabTitle(edited);

        if (_initialising || _busy || ReadAsYouTypeBox.IsChecked != true) return;

        var change = e.Changes.FirstOrDefault();
        if (change is null) return;

        var snippet = TypingReader.TextToRead(InputText.Text, change.Offset, change.AddedLength, change.RemovedLength);
        if (string.IsNullOrWhiteSpace(snippet)) return;

        SpeakSnippet(snippet!);
    }

    /// <summary>Queues one word and speaks it after any words already waiting.</summary>
    private async Task SpeakSnippetAsync(string snippet)
    {
        var engine = _engine;
        if (engine is null) return;

        // Drop words once the backlog is long enough to be useless.
        if (Interlocked.Increment(ref _queuedSnippets) > MaxQueuedSnippets)
        {
            Interlocked.Decrement(ref _queuedSnippets);
            return;
        }

        var token = (_typingCancellation ??= new CancellationTokenSource()).Token;
        var speakerId = _speakerId;
        var speed = (float)SpeedSlider.Value;

        try
        {
            await _typingTurnstile.WaitAsync(token);

            try
            {
                await Task.Run(() =>
                {
                    using var player = new AudioPlayerSink(engine.SampleRate, token);
                    engine.Synthesize(snippet, speakerId, speed, samples =>
                    {
                        if (token.IsCancellationRequested) return false;
                        player.WriteSamples(samples.Span);
                        return true;
                    }, token);
                    player.WaitUntilDrained();
                }, token);
            }
            finally
            {
                _typingTurnstile.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Stop, or a full read, cleared the queue.
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
        finally
        {
            Interlocked.Decrement(ref _queuedSnippets);
        }
    }

    private void CancelTypingPlayback()
    {
        var cancellation = _typingCancellation;
        _typingCancellation = null;
        cancellation?.Cancel();
    }

    private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        CancelTypingPlayback();
        _cancellation?.Cancel();
        _player?.Stop();
        _settings.Save();
        DisposeEngine();
        Media?.Dispose();
        CloseNotifications();
    }

    private void DisposeEngine()
    {
        _engine?.Dispose();
        _engine = null;
        _loadedVoiceName = null;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        Cursor = busy ? System.Windows.Input.Cursors.AppStarting : null;
        LockText(busy);
        LockPlaybackSettings(busy);
        ShowRestartState(busy && _rerun is not null);

        if (busy) Media?.Describe(TextTitleBox.Text);
        ShowMediaState(busy ? MediaState.Playing : MediaState.Stopped);
    }

    /// <summary>The text being spoken is held still, and looks it, until the run ends. It can
    /// still be scrolled and copied from.</summary>
    private void LockText(bool locked)
    {
        var document = _runDocument ?? ActiveDocument;
        if (document is null) return;

        LockBox(document.Box, locked);
        if (!locked) _runDocument = null;
    }

    /// <summary>Read becomes Restart while a reading is in flight, so Stop is not needed first.</summary>
    private void ShowRestartState(bool running)
    {
        ShowAction(ReadButton, running ? Glyph.Restart : Glyph.Play, running ? "Restart" : "Read");
        ShowAction(ReadFileButton, running ? Glyph.Restart : Glyph.Play, running ? "Restart" : "Read file");
    }

    private void SetStatus(string message)
    {
        StatusText.Inlines.Clear();
        StatusText.Text = message;
        RecordStatus(message);
    }

    /// <summary>Says what was written and makes the folder a link, since a path alone is not.</summary>
    private void SetStatusWithFileLink(string message, string path)
    {
        var folder = Path.GetDirectoryName(path);

        if (folder is null)
        {
            SetStatus(message + path);
            return;
        }

        var link = new Hyperlink(new Run(folder)) { ToolTip = "Open this folder" };
        link.Click += (_, _) => OpenFolder(folder);

        StatusText.Inlines.Clear();
        StatusText.Inlines.Add(new Run(message));
        StatusText.Inlines.Add(link);
        RecordStatus(message + folder);
    }

    private void OpenFolder(string folder)
    {
        try
        {
            FolderOpener(folder);
        }
        catch (Exception ex)
        {
            SetStatus($"Could not open {folder}: {ex.Message}");
        }
    }

    /// <summary>Puts the question about a tab with unsaved words in front of the reader.</summary>
    private (bool Close, bool StopAsking) AskAboutUnsavedClose(string message)
    {
        var dialog = new CloseTabWindow(message) { Owner = this };
        var closed = dialog.ShowDialog() == true;
        return (closed, dialog.StopAsking);
    }

    /// <summary>Offers a name for an audio file. Tests answer without a dialog.</summary>
    internal Func<string, string?> AudioPathPicker { get; set; }

    /// <summary>The words behind a recording are kept under the name given to the audio, so they
    /// can be read again. The setting turns it off.</summary>
    private void KeepTextAsScript(string text, string audioPath)
    {
        if (!_settings.SaveScriptWithAudio) return;

        var title = ScriptLibrary.ToFileName(Path.GetFileNameWithoutExtension(audioPath));
        if (title is null) return;

        try
        {
            var saved = Scripts.Save(title, text);
            KeepVoiceWithScript(saved.Title);
            ActiveScriptTitle = saved.Title;
            ScriptTitleBox.Text = saved.Title;
            MarkDocumentSaved(ActiveDocument);
            RefreshScripts();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus($"The audio is being written, but {title} could not be saved as a script.");
        }
    }

    /// <summary>The file name a script leads with, or the plain one when the text came from
    /// somewhere else.</summary>
    internal static string AudioStem(string? scriptTitle) =>
        ScriptLibrary.ToFileName(scriptTitle) ?? "tts_output";

    /// <summary>The script the Text tab holds, which names the audio written from it.</summary>
    internal string? TextScriptTitle
    {
        get => ActiveScriptTitle;
        set => ActiveScriptTitle = value;
    }

    /// <summary>Offers a file name in the configured format, inside the output folder.</summary>
    private string? AskForAudioPath(string suggestedStem)
    {
        var mp3 = _settings.OutputFormat == AudioOutputFormat.Mp3;

        var dialog = new SaveFileDialog
        {
            Title = mp3 ? "Write MP3 file" : "Write wave file",
            Filter = mp3
                ? "MP3 files (*.mp3)|*.mp3|Wave files (*.wav)|*.wav"
                : "Wave files (*.wav)|*.wav|MP3 files (*.mp3)|*.mp3",
            FileName = suggestedStem + (mp3 ? ".mp3" : ".wav"),
            AddExtension = true,
            DefaultExt = mp3 ? "mp3" : "wav",
        };

        var initial = EnsureOutputDirectory();
        if (initial is not null) dialog.InitialDirectory = initial;

        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    /// <summary>Creates the output folder so the dialog opens there the first time too.</summary>
    private string? EnsureOutputDirectory()
    {
        var directory = _settings.ResolvedOutputDirectory;

        try
        {
            Directory.CreateDirectory(directory);
            return directory;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Directory.Exists(directory) ? directory : null;
        }
    }

    /// <summary>The sink a run writes to, chosen by the file's own extension.</summary>
    private ISampleSink CreateFileSink(string path, int sampleRate, Action<string> progress) =>
        CreateFileSink(path, sampleRate, _settings.Mp3BitRate, progress);

    private static ISampleSink CreateFileSink(string path, int sampleRate, int bitRate, Action<string> progress)
    {
        var isMp3 = string.Equals(Path.GetExtension(path), ".mp3", StringComparison.OrdinalIgnoreCase);
        if (!isMp3) return new WaveFileSink(path, sampleRate);

        return new Mp3FileSink(path, sampleRate, bitRate, progress);
    }

    private static string? AskForDirectory(string? current)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current)) dialog.SelectedPath = current;
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }

    private static TextReader OpenTextFile(string path) =>
        new StreamReader(path, detectEncodingFromByteOrderMarks: true);

    private static string ReadLicenseSummary(VoiceDescriptor voice)
    {
        var path = voice.LicensePath ?? voice.ModelCardPath;
        if (path is null || !File.Exists(path)) return string.Empty;

        try
        {
            var text = File.ReadAllText(path);
            return text.Length > 4000 ? text[..4000] + "..." : text;
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    private const int MaxQueuedSnippets = 8;

    private static int ParseInt(string text, int fallback) =>
        int.TryParse(text.Trim(), out var value) ? value : fallback;

    /// <summary>An empty or unreadable box means automatic, which the settings carry as null.</summary>
    private static int? ParseOptionalInt(string text) =>
        int.TryParse(text.Trim(), out var value) ? value : null;
}
