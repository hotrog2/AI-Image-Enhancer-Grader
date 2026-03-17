# AI Image Enhancer Grader

`AI Image Enhancer Grader` is a local-first Windows photo enhancement and color grading app built with `WPF + C#`.

It is designed for photographers who want:
- offline editing
- fast library import and cataloging
- one-click enhancement with selectable features
- manual fine-tuning after the auto pass
- reusable presets
- style learning from accepted edits
- batch export

The current app runs fully on the local machine and does not require a cloud account or hosted API.

## Status

This repository is an active prototype/MVP.

What is working now:
- Windows desktop app built with `WPF` on `.NET 9`
- local SQLite catalog
- folder import and image library view
- cached thumbnails
- one-click enhancement preview
- feature toggles for enhancement passes
- manual fine-tune sliders
- saved enhancement presets
- style profiles with accept/decline feedback learning
- batch export to `JPG` and `PNG`
- recent export history
- Windows packaging flow with portable and installer zip outputs
- install and uninstall PowerShell scripts
- JPEG, PNG, and RAW catalog support
- RAW decode chain using `WIC` and `LibRaw`

What is still incomplete:
- true production-grade RAW workflow validation across real camera samples
- advanced localized masking
- crop / straighten tools
- GPU inference models through `ONNX Runtime + DirectML`

## Tech Stack

- `C#`
- `.NET 9`
- `WPF`
- `SQLite`
- `CommunityToolkit.Mvvm`
- `ImageSharp`
- `LibRaw` via `HurlbertVisionLab.LibRawWrapper`

## Current Features

### Library and Catalog

- Import folders recursively into a local catalog
- Persist metadata in SQLite
- Cache thumbnails under local app data
- Keep recent export history

### Enhancement Workflow

- One-click enhancement with these selectable features:
  - auto exposure
  - white balance
  - contrast shaping
  - tone curve / color pop
  - skin finish
  - denoise
  - sharpen
  - upscale flag
  - style learning
- Enhancement strength slider
- Before/after preview tabs
- Manual fine-tune controls for:
  - exposure
  - contrast
  - warmth
  - saturation
  - vibrance
  - highlights
  - shadows
  - skin softening
  - denoise
  - sharpen

### Learning and Reuse

- Save named enhancement presets
- Create named style profiles
- Accept or decline results to train a selected profile
- Fall back to catalog-wide history when no style profile is selected

### Export

- Export selected image or entire catalog
- Export format: `JPEG` or `PNG`
- Configurable long edge size
- Configurable JPEG quality
- Queue view with per-item status

## RAW Support

The app currently catalogs these RAW extensions:

- `CR2`
- `CR3`
- `NEF`
- `ARW`
- `DNG`
- `RW2`
- `ORF`

Important: RAW support is still early.

The current pipeline tries:
1. Windows `WIC` support
2. `LibRaw` decode
3. matching `JPG/PNG` proxy fallback when available

This is enough to scaffold the workflow, but it has not yet been validated against a broad set of real camera RAW files.

## Project Layout

```text
src/
  ColorGrader.App             WPF desktop UI
  ColorGrader.Application     workflow orchestration and interfaces
  ColorGrader.Core            domain models and shared logic
  ColorGrader.Infrastructure  SQLite catalog and thumbnail cache
  ColorGrader.Imaging         image pipeline and RAW decoding
  ColorGrader.AI              style-learning service
tests/
  ColorGrader.Tests           xUnit tests
```

## Requirements

- Windows 10 or Windows 11
- `x64` machine
- `.NET 9 SDK`
- Visual Studio 2022 or newer, or the `dotnet` CLI

## Getting Started

### Build

```powershell
dotnet build ColorGrader.sln
```

### Run Tests

```powershell
dotnet test ColorGrader.sln
```

### Run the App

```powershell
dotnet run --project .\src\ColorGrader.App\ColorGrader.App.csproj
```

## Packaging

The repo now includes a Windows packaging flow under `build/`.

### Build Release Packages

```powershell
.\build\Package-ColorGrader.ps1
```

That script produces:
- `artifacts\publish\win-x64\` with the self-contained published app
- `artifacts\packages\ColorGrader-win-x64-portable.zip`
- `artifacts\packages\ColorGrader-win-x64-installer.zip`

### Install From the Installer Package

After extracting `ColorGrader-win-x64-installer.zip`:

```powershell
.\Install-ColorGrader.ps1
```

Optional desktop shortcut:

```powershell
.\Install-ColorGrader.ps1 -CreateDesktopShortcut
```

Default install location:

```text
%LocalAppData%\Programs\ColorGrader
```

## Local Storage

The app stores its local data here:

```text
%LocalAppData%\ColorGrader
```

That folder currently contains:
- `catalog.db` for SQLite catalog data
- `thumbs\` for cached thumbnails

## Notes

- The app is local-first and intended for lawful photography workflows.
- No cloud account is required.
- The current enhancement logic is deterministic and feedback-driven; it is not yet using a dedicated GPU inference model.
- The codebase is already structured so `DirectML` / `ONNX Runtime` integration can be added later.

## Roadmap

- validate RAW processing against real camera files
- add crop and straighten
- add localized masking and skin-aware region tools
- add stronger model-based denoise / upscale passes
- add publish profiles for Windows release builds

## Verification

Latest verified commands:

```powershell
dotnet build ColorGrader.sln
dotnet test ColorGrader.sln
.\build\Package-ColorGrader.ps1
```
