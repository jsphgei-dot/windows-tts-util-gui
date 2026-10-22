/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using System.IO;
using TtsUtil.Core.Text;
using TtsUtil.Core.Tts;

namespace TtsUtil.App;

/// <summary>Writing a document to audio in the background, each job on a voice of its own so
/// several tabs can be written at once while a reading carries on.</summary>
public partial class MainWindow
{
    private readonly List<WriteJob> _writeJobs = new();

    /// <summary>The writes still running. Tests wait on them.</summary>
    internal IReadOnlyList<WriteJob> WriteJobs => _writeJobs;

    /// <summary>Starts writing a document to audio and hands the window straight back.</summary>
    internal Task WriteInBackgroundAsync(TextDocument document, string text, string path)
    {
        var voice = SelectedVoice;
        if (voice is null)
        {
            SetStatus("Select a voice first.");
            return Task.CompletedTask;
        }

        if (_writeJobs.Any(running => ReferenceEquals(running.Document, document)))
        {
            SetStatus($"{document.Title} is already being written.");
            return Task.CompletedTask;
        }

        var job = new WriteJob(document, path);
        _writeJobs.Add(job);
        LockBox(document.Box, true);
        ShowJobProgress(job, 0);

        var loader = EngineLoader;
        var threads = _settings.ResolvedNumThreads;
        var options = _settings.ToChunkerOptions();
        var aliases = ActiveAliases();
        var speakerId = _speakerId;
        var speed = (float)SpeedSlider.Value;
        var bitRate = _settings.Mp3BitRate;
        var token = job.Cancellation.Token;
        var progress = new Progress<SynthesisProgress>(step => ShowJobProgress(job, step.Percent));

        return job.Work = FinishAsync(job, Task.Run(() =>
        {
            using var engine = loader(voice, threads);
            using var reader = AliasTextReader.Wrap(new StringReader(text), aliases);
            var runner = new SynthesisRunner(engine, options) { SpeakerId = speakerId, Speed = speed };

            // Disposing encodes when the sink is an MP3 one, on this worker thread.
            var sink = CreateFileSink(path, engine.SampleRate, bitRate, _ => { });
            try
            {
                runner.Run(reader, text.Length, sink, progress, token);
            }
            finally
            {
                (sink as IDisposable)?.Dispose();
            }

            return runner.UtterancesSpoken;
        }, token));
    }

    /// <summary>Stops a write, and deletes the part written when asked to.</summary>
    internal bool CancelWriteFor(TextDocument document, bool deleteFile = false)
    {
        var jobs = _writeJobs.Where(running => ReferenceEquals(running.Document, document)).ToList();
        foreach (var job in jobs)
        {
            job.DeleteWhenStopped |= deleteFile;
            job.Cancellation.Cancel();
        }

        return jobs.Count > 0;
    }

    private async Task FinishAsync(WriteJob job, Task<long> work)
    {
        try
        {
            var spoken = await work;
            if (spoken == 0)
            {
                SetStatus($"Nothing in {job.Document.Title} was left to read: every character was filtered.");
            }
            else
            {
                SetStatusWithFileLink($"Wrote {Path.GetFileName(job.Path)}. ", job.Path);

                // Documents written side by side announce themselves once, on the last one.
                if (_writeJobs.Count == 1) NotifyWriteFinished(job.Path);
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus(job.DeleteWhenStopped
                ? DeletePartWritten(job)
                : $"Stopped writing {job.Document.Title}.");
        }
        catch (Exception exception)
        {
            SetStatus($"Could not write {job.Document.Title}: {exception.Message}");
        }
        finally
        {
            _writeJobs.Remove(job);
            job.Cancellation.Dispose();
            LockBox(job.Document.Box, false);
            job.Document.Label.Text = job.Document.Title;
        }
    }

    private static string DeletePartWritten(WriteJob job)
    {
        try
        {
            File.Delete(job.Path);
            return $"Stopped writing {job.Document.Title} and deleted {Path.GetFileName(job.Path)}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"Stopped writing {job.Document.Title}, but {Path.GetFileName(job.Path)} could not be deleted: {exception.Message}";
        }
    }

    /// <summary>A running write shows its progress on its own tab, out of the way of the rest.</summary>
    private void ShowJobProgress(WriteJob job, int percent)
    {
        job.Percent = percent;
        job.Document.Label.Text = $"{job.Document.Title}  {percent}%";
    }
}

/// <summary>One document being written to audio.</summary>
internal sealed class WriteJob
{
    public WriteJob(TextDocument document, string path)
    {
        Document = document;
        Path = path;
    }

    public TextDocument Document { get; }

    public string Path { get; }

    public CancellationTokenSource Cancellation { get; } = new();

    public Task Work { get; set; } = Task.CompletedTask;

    public int Percent { get; set; }

    public bool DeleteWhenStopped { get; set; }
}
