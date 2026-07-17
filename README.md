# 🏭 TransCoda for Windows 🎬

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Windows-11%20%7C%2010%2019041+-0078D4?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![PRs](https://img.shields.io/badge/PRs-welcome-brightgreen)](CONTRIBUTING.md)

**Fast, tiny, fully native media conversion for Windows 11.** ⚡

TransCoda converts video and audio files with FFmpeg behind a clean, native
WinUI 3 interface. No Electron, no web runtime, no bundled browser — it looks,
feels, and performs like it belongs on your PC: Mica backdrop, Fluent
controls, dark/light theme awareness, drag & drop straight from File Explorer. 🪟✨

> 🔍 Looking for the macOS app? See
> [TransCoda4Mac](https://github.com/albertolicea00/TransCoda4Mac) —
> an independent repository with the same architecture built natively on
> SwiftUI.

## 🧠 Why native?

A media converter spends its resources on one thing: **encoding**. The UI should
cost almost nothing. Design goals, in order:

1. **📦 Minimal footprint.** Idle RAM in the tens of megabytes, near-zero idle
   CPU, small binary. Every resource the app doesn't use is a resource FFmpeg can.
2. **🎨 Platform integration.** Follows the Windows 11 design language (Fluent,
   Mica) instead of shipping a lowest-common-denominator UI.
3. **🔧 Simple, honest engine.** The app is a thin native shell around FFmpeg.
   You can read the exact command each conversion runs.

## ✨ Features

- 📋 **Batch conversion queue** with per-file progress, cancel, and reveal in File Explorer.
- 🎞️ **Video containers:** MP4, MKV, MOV, WebM.
- 🎥 **Video codecs:** H.264/AVC, H.265/HEVC, AV1, VP9.
- 🎵 **Audio-only output:** M4A (AAC), MP3, FLAC, Opus, WAV.
- 🎚️ **Quality control** with a CRF-style slider, resolution limiting, audio bitrate selection.
- 🚀 **Hardware-accelerated encoding**, auto-detected from the local FFmpeg build:
  NVIDIA NVENC, Intel Quick Sync, AMD AMF — picked in that order per codec.
- 📊 **Accurate progress** from FFmpeg's machine-readable `-progress` stream.
- 📁 **Output** next to the source file or to a folder you choose; never overwrites
  existing files.

## ⚙️ How it works

```
┌─────────────────────────────┐
│  WinUI 3 (Mica, Fluent)     │  queue list, drag & drop, settings pane
├─────────────────────────────┤
│  JobQueue                   │  ordered processing, one encode at a time
│  FfmpegCommandBuilder       │  settings → argument list (no shell involved)
│  TranscodeEngine            │  spawns ffmpeg, parses -progress, cancellation
│  HardwareCapabilities       │  parses `ffmpeg -encoders` once at startup
│  Ffprobe                    │  duration probe for progress percentage
│  FfmpegLocator              │  app folder → known paths → PATH
└─────────────────────────────┘
            │
            ▼
        ffmpeg.exe / ffprobe.exe  (external processes)
```

FFmpeg binaries are **not** committed to this repository. The app looks for
them in this order: next to `TransCoda.exe` (or in an `ffmpeg\` subfolder),
`C:\ffmpeg\bin`, then `PATH`.

## 📥 Getting FFmpeg

```powershell
winget install Gyan.FFmpeg
```

or download a build from [gyan.dev](https://www.gyan.dev/ffmpeg/builds/) and
drop `ffmpeg.exe` and `ffprobe.exe` next to `TransCoda.exe`. AV1 software
encoding uses `libsvtav1`, included in the standard Gyan.dev build.

## 🔨 Building

Requirements: Windows 11 (or Windows 10 19041+), Visual Studio 2022 with the
**.NET desktop development** and **Windows App SDK** workloads — or just the
.NET 8 SDK on the command line.

```powershell
dotnet build TransCoda.sln
```

Or open `TransCoda.sln` in Visual Studio and press F5. The app runs unpackaged
(`WindowsPackageType=None`) with the Windows App SDK self-contained, so no
MSIX deployment step is needed during development.

## 📂 Project layout

```
TransCoda.sln
TransCoda/
  App.xaml(.cs)        app entry point
  MainWindow.xaml(.cs) queue window, settings pane, drag & drop
  Models/              formats, codecs, settings, job model
  Core/                locator, prober, command builder, engine, queue
```

## 🗺️ Roadmap

- [ ] 💾 Presets (save/load named conversion profiles)
- [ ] 📝 Subtitle pass-through and burn-in
- [ ] 🌈 HDR metadata pass-through
- [ ] ✂️ Trim / clip range selection
- [ ] 📦 Signed MSIX releases

## 🤝 Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). This project follows the
[Contributor Covenant](CODE_OF_CONDUCT.md) code of conduct and uses
[Conventional Commits](https://www.conventionalcommits.org/).
