/*
 * TTS Util Win
 *
 * Licensed under the Apache License, Version 2.0.
 */

using TtsUtil.Core.Tts;

namespace TtsUtil.Cli.Audio;

/// <summary>A sink that holds something worth closing, such as a file or an encoder.</summary>
internal interface IDisposableSink : ISampleSink, IDisposable
{
}
