[Documentation index](index.md) | [Usage](usage.md) | [Performance](performance.md) | [Build](building.md) | [Release](releasing.md) | [Develop](developing.md) | [Write a feature](writing-a-feature.md)

---

# Speed and machine requirements

What the program actually costs to run, measured rather than guessed, and what the
**Synthesis threads** setting does with that.

## The machine everything below was measured on

AMD Ryzen 7 5800X3D, eight physical cores and sixteen logical, 3.4 GHz, 32 GB of RAM, Windows
11, the x64 build. One machine is one machine: the numbers are there to show the shape of the
curve, not to promise a result on different hardware.

The measurement loads a voice, synthesizes the same passage of about 90 words, and divides the
seconds of audio produced by the seconds of work it took. A result of 4.0x means four seconds of
speech per second of rendering. Playback needs 1.0x, so anything above that keeps ahead of the
listener and the rest is how quickly a file is written.

## Downloaded voices, by thread count

| Voice | 1 thread | 2 | 4 | 8 |
| --- | --- | --- | --- | --- |
| kokoro en v0.19, the heaviest offered | 1.4x | 2.4x | 3.7x | 4.3x |
| piper en_US ljspeech high | 1.7x | 3.1x | 5.1x | 6.8x |
| piper en_US libritts_r medium | 13.6x | 22.7x | 32.4x | 35.5x |

Four threads is 76% to 91% of what eight threads give, for half the machine. That flattening is
the whole reason the automatic count stops where it does. Beyond eight the curve is flatter
still, and the threads spend their time synchronizing rather than rendering.

## The Windows voices

Microsoft David, Microsoft Mark and Microsoft Zira measured between 372x and 592x on the same
passage, and the thread setting changed nothing at any value.

That is not an oddity to explain away. Those voices are rendered by the Windows speech service
rather than by this program, `WindowsTtsEngine.Load` takes no thread count, and the concatenative
technique behind them is a fraction of the work a neural model does. The trade is quality: they
are the robotic ones. Nothing on this page applies to them.

## How the automatic thread count is worked out

Leave **Synthesis threads** empty and `ThreadPlan` in Core decides, once, when a voice loads. It
reads `Environment.ProcessorCount` and nothing else.

| Logical processors | Automatic | Left free |
| --- | --- | --- |
| 2 | 1 | 1 |
| 4 | 2 | 2 |
| 6 | 3 | 3 |
| 8 | 4 | 4 |
| 16 | 4 | 12 |
| 32 or more | 8 | the rest |

Half the processors, at least one, at most four, and at most eight once the machine reports 32
or more, where there are cores to spare and the last of the speed is worth taking. The x86 build
doubles whichever ceiling applies, as it renders less per thread than the x64 one.

Typing a number overrides all of it, anywhere from 1 to 16. That range is fixed so that a
settings file means the same thing on every machine, and 16 is already twice the point where
the gains stop paying.

## Minimum and recommended

| | Minimum | Recommended |
| --- | --- | --- |
| Windows | 10 version 1607 | 11, or 10 22H2 |
| Processor | two cores | four cores or more |
| Memory | 4 GB | 8 GB |
| Free disk | 250 MB for the program | that, plus 60 to 300 MB for each neural voice |

The minimum column is what runs: the Windows voices, the smaller neural voices, and reading
along with the text on screen. On Windows 10 builds before 1709 the voices Windows already
has read at their own speed, which is where rate control was added to them. The neural voices
take their speed from the program and are unaffected. The recommended column is what keeps the heaviest neural voice
comfortably ahead of playback and makes writing an MP3 of a long script quick rather than
something to walk away from.

## Reproducing this

There is no benchmark in the repository. The figures above came from a throwaway console project
referencing `TtsUtil.Core` and `TtsUtil.App`, loading each voice through `SherpaTtsEngine.Load`
and `WindowsTtsEngine.Load`, timing `Synthesize` and counting the samples handed to the callback.
Anything measuring the same two calls will do, and the numbers are worth remeasuring whenever
the sherpa-onnx version moves.
