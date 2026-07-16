# Architecture

TransCoda for Windows is a thin native shell around FFmpeg. One window, one
queue, one external process at a time. This document describes the moving
parts and the reasoning behind them.

## Overview

```
┌───────────────────────────────────────────────────────────┐
│ WinUI 3 (MainWindow.xaml + code-behind)                   │
│   command bar ── add / convert / clear                    │
│   ListView ── x:Bind to ConversionJob (INPC)              │
│   settings pane ── combos/slider write ConversionSettings │
│   Mica backdrop, custom title bar, drag & drop            │
└───────────────▲───────────────────────────────────────────┘
                │ ObservableCollection + INotifyPropertyChanged
┌───────────────┴───────────────────────────────────────────┐
│ JobQueue (UI thread)                                      │
│   owns Jobs, ConversionSettings, output dir               │
│   serial processing loop (StartAsync)                     │
└──┬──────────┬──────────────┬──────────────────────────────┘
   │          │              │
   │   Ffprobe (duration)    │
   │          │              │
   │   FfmpegCommandBuilder  │  settings → argument list
   │          │              │
┌──▼──────────▼──────────────▼───────────┐
│ TranscodeEngine                        │  one instance per running job
│   spawns ffmpeg, parses -progress,     │
│   drains stderr, handles cancellation  │
└──────────────────┬─────────────────────┘
                   │ Process
           ffmpeg.exe / ffprobe.exe
   (located once at startup by FfmpegLocator;
    capabilities read once by HardwareCapabilities)
```

## Components

### FfmpegLocator (`Core/FfmpegLocator.cs`)

Finds `ffmpeg.exe`/`ffprobe.exe` once at startup. Order: next to the app
executable → `ffmpeg\` / `ffmpeg\bin` subfolders → `C:\ffmpeg\bin` → every
PATH entry (covers winget/choco/scoop shims). Returns null when nothing is
found; the UI shows an InfoBar and disables conversion instead of failing later.

### HardwareCapabilities (`Core/HardwareCapabilities.cs`)

Runs `ffmpeg -hide_banner -encoders` once and keeps the set of `*_nvenc`,
`*_qsv`, `*_amf` encoder names. `Encoder(codec)` picks the best available
hardware encoder in **NVENC → QSV → AMF** order (H.264, HEVC, AV1); VP9 always
falls back to software. Detection is data-driven from the actual FFmpeg build,
not assumed from installed GPUs — a build compiled without NVENC degrades
gracefully.

### FfmpegCommandBuilder (`Core/FfmpegCommandBuilder.cs`)

Pure mapping from `(input, output, settings, hardware)` to an argument list.
The only place FFmpeg arguments are constructed. Key decisions encoded here:

- Software encoders use CRF (`libx264`/`libx265`: `-crf`, `libsvtav1`:
  `-crf` + `-preset 8`, `libvpx-vp9`: `-crf` + `-b:v 0` + `-row-mt 1`).
- Hardware encoders have no `-crf`; each family gets its own constant-quality
  knob — NVENC: `-rc vbr -cq <q> -b:v 0`, QSV: `-global_quality <q>`,
  AMF: `-rc cqp -qp_i <q> -qp_p <q>`.
- Resolution limiting uses `scale=-2:min(ih\,H)` — `-2` keeps width even
  (encoder requirement), `min()` prevents upscaling.
- HEVC in MP4/MOV gets `-tag:v hvc1` so Apple devices play the result.
- MP4/MOV/M4A get `-movflags +faststart`.
- `-progress pipe:1 -nostats -loglevel error` keeps stdout machine-readable
  and stderr small.

### TranscodeEngine (`Core/TranscodeEngine.cs`)

Owns one FFmpeg `Process` for one job. Responsibilities:

- **Progress**: reads stdout line by line; `out_time_us=` (or the misnamed
  `out_time_ms=`, also microseconds) divided by the probed duration gives the
  fraction. Capped at 0.999 until the exit code confirms success. Parsed with
  `InvariantCulture`.
- **Stall prevention**: stderr is drained concurrently into a 4 KB tail.
  Without this, a chatty encode fills the pipe buffer and FFmpeg blocks.
- **Cancellation**: `Cancel()` flips a flag under a lock and kills the process
  tree; the run loop then surfaces `OperationCanceledException` instead of a
  spurious failure, and the queue deletes the partial output file.

### JobQueue (`Core/JobQueue.cs`)

Owns all app state; every member is UI-thread-only. Processes jobs
**serially**: one FFmpeg encode already saturates the GPU encoder or CPU, and
a serial loop keeps memory flat for arbitrarily large queues. Other behaviors:

- Settings are captured per job at **start time**, so tweaking the pane
  mid-batch affects the not-yet-started remainder of the queue.
- Output naming: source name with the new extension, in the source folder (or
  a chosen folder); ` 2`, ` 3`… suffixes instead of overwriting. FFmpeg gets
  `-y` because the queue has already guaranteed the path is free.
- Failed/cancelled jobs delete their partial output.
- Duplicate detection: a file already pending is not enqueued twice; finished
  jobs don't block re-adding the same file.
- `StateChanged` event drives command enablement in the window; per-job UI
  updates flow through `INotifyPropertyChanged` instead.

### Models (`Models/`)

`MediaFormats` extension methods are the source of truth for container/codec
compatibility (`SupportedVideoCodecs`, `SupportedAudioCodecs`).
`ConversionSettings.Normalized()` clamps any codec choice to the container
before a job runs — the settings pane filters its combos with the same lists,
so normalization is a backstop, not the primary UX.

## Threading model

- `JobQueue` and `ConversionJob` are touched only from the UI thread.
- `TranscodeEngine.RunAsync` awaits on thread-pool continuations; progress
  reports marshal back through a `Progress<double>` constructed on the UI
  thread (WinUI installs a `DispatcherQueueSynchronizationContext`).
- Short-lived probes (`Ffprobe`, `HardwareCapabilities`) run through
  `ProcessRunner`, a one-shot async process wrapper.

## Deliberate trade-offs

| Decision | Why |
| --- | --- |
| External FFmpeg, not linked libav* | Tiny app, license simplicity, user-upgradable FFmpeg, crash isolation — an encoder crash can't take the app down. |
| Serial queue | Predictable resource usage; parallelism would thrash a single GPU encoder. A concurrency limit is on the roadmap. |
| Unpackaged + self-contained App SDK | F5/`dotnet run` development with zero MSIX friction; packaged signed releases are roadmap. |
| Code-behind instead of MVVM framework | Zero dependencies and less indirection at this app size; `Core/` stays UI-free so a ViewModel layer can be introduced later if the UI grows. |
| No persistence | Queue is session-scoped by design; presets/persistence are roadmap items. |
| Settings captured at job start | Least surprising batch behavior: what you see in the pane is what the next job gets. |
