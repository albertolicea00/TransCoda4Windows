# CLAUDE.md

This file provides guidance to AI coding agents when working with code in this repository.

## Project overview

TransCoda for Windows — a native WinUI 3 (C#/.NET 8) app that converts
video/audio files by driving an external FFmpeg process. Thin shell around
FFmpeg; the app itself must stay tiny and idle-cheap. Sister project
(independent repo, same architecture on SwiftUI):
[TransCoda4Mac](https://github.com/albertolicea00/TransCoda4Mac).

## Build & run

```powershell
dotnet build TransCoda.sln     # or open in Visual Studio 2022 and F5
```

- Requires the **.NET desktop development** + **Windows App SDK** VS workloads
  (or the .NET 8 SDK for CLI builds). Windows 10 19041+ target.
- Runs **unpackaged** (`WindowsPackageType=None`) with the Windows App SDK
  self-contained — no MSIX deployment during development.
- FFmpeg: `winget install Gyan.FFmpeg`, or drop `ffmpeg.exe`/`ffprobe.exe`
  next to the built `TransCoda.exe`.

## Layout

```
TransCoda.sln
TransCoda/
  App.xaml(.cs)          entry point; owns the singleton JobQueue
  MainWindow.xaml(.cs)   queue window, settings pane, drag & drop, Mica
  Models/                OutputFormat/VideoCodec/AudioCodec enums + MediaFormats,
                         ConversionSettings, ConversionJob (INotifyPropertyChanged)
  Core/                  FfmpegLocator, Ffprobe, HardwareCapabilities,
                         FfmpegCommandBuilder, TranscodeEngine, JobQueue
```

See [ARCHITECTURE.md](ARCHITECTURE.md) for how the pieces interact.

## Hard rules

- **Native only.** WinUI 3 / Windows App SDK. No web views, no cross-platform
  UI layers, no NuGet dependencies beyond the Windows App SDK. A PR that adds
  a dependency needs an extraordinary reason.
- **FFmpeg args go through `ProcessStartInfo.ArgumentList`**, never
  concatenated command strings. No `cmd /c`, no quoting user paths by hand.
- **UI logic in code-behind; process logic in `Core/`.** `MainWindow.xaml.cs`
  may populate combos and toggle visibility; it never spawns processes.
- **UI thread ownership.** `JobQueue` and `ConversionJob` members are called
  from the UI thread only; engine progress marshals back via `Progress<double>`
  created on the UI thread. Keep it that way.
- Behavior changes (formats, codecs, FFmpeg arguments, queue semantics) should
  stay mirrored with TransCoda4Mac; flag divergence in the PR.

## Gotchas (learned, don't re-litigate)

- FFmpeg's `-progress` line `out_time_ms=` is **microseconds** despite the
  name; `out_time_us=` is preferred when present. Parsing handles both
  (`TranscodeEngine.ParseOutTimeSeconds`).
- Hardware encoders have **no `-crf`** — per-family quality knobs live in
  `FfmpegCommandBuilder`: NVENC `-rc vbr -cq`, QSV `-global_quality`,
  AMF `-rc cqp -qp_i/-qp_p`. Selection order per codec: NVENC → QSV → AMF.
- stderr must be drained concurrently during an encode or FFmpeg stalls on a
  full pipe buffer (`TranscodeEngine`).
- Pickers (`FileOpenPicker`/`FolderPicker`) need
  `InitializeWithWindow.Initialize(picker, hwnd)` in WinUI 3 desktop apps, and
  `FolderPicker` needs `FileTypeFilter.Add("*")`.
- Always parse FFmpeg/ffprobe numbers with `CultureInfo.InvariantCulture` —
  user locales with comma decimals break `double.Parse`.
- FFmpeg binaries are never committed. Lookup order: app folder →
  `ffmpeg\`/`ffmpeg\bin` subfolders → `C:\ffmpeg\bin` → PATH (`FfmpegLocator`).

## Commits

Conventional Commits, no scope needed (single-platform repo):
`feat: …`, `fix: …`, `docs: …`, `chore: …` — lower case, imperative, no period.
No AI attribution or Co-Authored-By trailers in commit messages.
