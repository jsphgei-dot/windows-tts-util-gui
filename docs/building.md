[Documentation index](index.md) | [Usage](usage.md) | [Performance](performance.md) | [Build](building.md) | [Release](releasing.md) | [Develop](developing.md) | [Write a feature](writing-a-feature.md)

---

# 🛠️ Build from source

Building takes about a minute and needs only the .NET 10 SDK.

```powershell
# 1. Download voices (about 490 MB for the three permissively licensed defaults).
.\scripts\FetchVoices.ps1 -Default

# 2. Build the portable executable into dist\TtsUtilWin-x64.
.\scripts\BuildPortable.ps1

# 3. Run it.
.\dist\TtsUtilWin-x64\TtsUtilWin.exe
```

The installer needs [Inno Setup 6](https://jrsoftware.org/isinfo.php) as well:
`winget install JRSoftware.InnoSetup`. The build script finds it automatically, whether winget
installed it per user or per machine.

```powershell
.\scripts\BuildPortable.ps1                 # portable folder only
.\scripts\BuildPortable.ps1 -Installer      # portable folder and the setup program
.\scripts\BuildPortable.ps1 -Installer -IncludeVoices   # also copy the voice models in
```

| What | Path | Size |
| --- | --- | --- |
| Portable folder | `dist\TtsUtilWin-<arch>\` | 82 MB, plus voices |
| Portable executable | `dist\TtsUtilWin-<arch>\TtsUtilWin.exe` | 82 MB |
| Installer | `dist\TtsUtilWin-<version>-<arch>-setup.exe` | 77 MB |

## The three architectures

`-Runtime` picks the processor, and each one writes to its own folder and its own installer
filename, so all three can sit in `dist` at once. Any of them can be cross built from any
Windows machine.

```powershell
.\scripts\BuildPortable.ps1 -Runtime win-x64 -Installer     # the default
.\scripts\BuildPortable.ps1 -Runtime win-arm64 -Installer   # Snapdragon and other ARM machines
.\scripts\BuildPortable.ps1 -Runtime win-x86 -Installer     # 32 bit Windows
```

| Runtime | Executable | Installs into | Runs on |
| --- | --- | --- | --- |
| `win-x64` | 82 MB | `Program Files` | x64, and ARM64 under emulation |
| `win-arm64` | 78 MB | `Program Files` | ARM64 only, natively |
| `win-x86` | 76 MB | `Program Files (x86)` | anything 32 bit or better |

The sherpa-onnx native libraries ship for all three, so the speech engine is native in each
build rather than emulated. The installer refuses a machine it does not match, which is why the
ARM64 build cannot be run on an x64 machine by mistake.

Only one of them can be installed at a time. They share an application id, a program folder and
a Start menu entry, so a setup that finds another architecture already installed offers to
remove it first and ends rather than installing beside it. Each install records the
architecture it was built for under `InstalledArch` in its uninstall key, which is how the next
setup knows. An ARM64 machine runs all three setups, so that check is the only thing keeping two
copies apart there. A 32 bit setup is redirected to `WOW6432Node`, so the check reads the 64 bit
view, the 32 bit view and `HKEY_CURRENT_USER`, covering per machine and per user installs
alike.

| Switch | Effect |
| --- | --- |
| `-Installer` | Compiles `installer\TtsUtilWin.iss` after publishing |
| `-IncludeVoices` | Copies `voices\` into the portable folder, so it can be zipped and handed over whole |
| `-SkipTests` | Skips both test suites. The build normally refuses to publish if any test fails |
| `-OutputDirectory <path>` | Publishes somewhere other than `dist\TtsUtilWin-<arch>` |
| `-Runtime` | `win-x64`, `win-arm64` or `win-x86`, defaulting to `win-x64` |
| `-Configuration` | Defaults to `Release` |

The version in the installer filename, in its Apps entry and in the executable's file
properties all come from `Directory.Build.props`. Nothing needs editing in two places, and
nothing in a pull request needs to touch that file: see [releasing.md](releasing.md) for how a
build reaches other people.
