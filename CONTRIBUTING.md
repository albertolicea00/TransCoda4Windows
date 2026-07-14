# Contributing to TransCoda for Windows

Thanks for your interest in improving TransCoda. This document explains how the
project is organized, how to set up a development environment, and the
conventions pull requests are expected to follow.

## Project philosophy

Two rules drive every decision in this codebase:

1. **Stay native.** C# + WinUI 3 only. No cross-platform UI layers, no web
   views, no runtime dependencies beyond the platform SDK. If a feature needs
   a heavyweight dependency, it probably doesn't belong here.
2. **Stay lean.** The app is a thin shell around FFmpeg. Prefer small,
   readable code over abstractions; prefer doing nothing at idle over
   background work.

## Sister project

[TransCoda4Mac](https://github.com/albertolicea00/TransCoda4Mac) is an
independent repository implementing the same architecture natively on SwiftUI.
The repos share no code, but they intentionally mirror each other's behavior
(locator → prober → command builder → engine → queue). If your change alters
user-visible behavior or FFmpeg command generation, consider opening a
matching issue there.

## Development setup

1. Install Visual Studio 2022 with the **.NET desktop development** and
   **Windows App SDK** workloads.
2. Install FFmpeg (`winget install Gyan.FFmpeg`) or place
   `ffmpeg.exe`/`ffprobe.exe` next to the built `TransCoda.exe`.
3. Open `TransCoda.sln` and run.

## Coding style

- Follow the standard [.NET naming and coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
  with nullable reference types enabled.
- UI logic lives in code-behind; anything that talks to a process lives in `Core/`.
- FFmpeg arguments are always built via `ProcessStartInfo.ArgumentList`, never
  concatenated into shell strings.
- Comments explain constraints and non-obvious FFmpeg behavior, not what the
  next line does.

## Commit convention

This repository uses [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/):

```
<type>: <description>
```

- **Types**: `feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `chore`, `ci`.
- Description in lower case, imperative mood, no trailing period.

Examples:

```
feat: add subtitle pass-through option
fix: keep progress bar visible while probing
docs: document hardware encoder detection order
```

## Pull requests

1. Fork and create a branch from `main`: `feat/short-description` or `fix/short-description`.
2. Keep PRs focused — one feature or fix per PR.
3. Make sure the app builds and a real conversion succeeds
   (any small video file is fine; describe what you tested in the PR).
4. Fill in the pull request template.
5. PRs that change FFmpeg command generation must list the exact argument
   changes in the description — these are the most sensitive lines in the app.

## Reporting bugs

Use the bug report template. The three most useful things you can include:

1. Windows version and app version.
2. Output of `ffmpeg -version`.
3. The failing file's characteristics (`ffprobe <file>` output) if you can share it.

## Questions

Open a GitHub Discussion or an issue with the question label.
