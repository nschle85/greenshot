# Greenshot macOS Port

This repository is a fork of the official Greenshot repository.

Upstream repository:
https://github.com/greenshot/greenshot

Development branch:
`macos-port`

## Goal

Create a native macOS / Apple Silicon version of Greenshot while preserving as much useful logic and behavior from the original open-source Greenshot project as practical.

The macOS implementation should remain compatible with future changes from `upstream/main`.

## Important upstream compatibility rule

Do not unnecessarily modify, rename, move, reformat, or delete existing Greenshot files.

Preserve the existing upstream repository structure whenever possible so that future merges from `upstream/main` remain manageable.

Prefer adding new projects and extracting reusable functionality incrementally.

Do not perform repository-wide namespace changes, formatting changes, project conversions, or file moves without explicit approval.

## Target platform

Primary target:

* macOS
* Apple Silicon arm64

Intel macOS support may be considered later.

## Preferred technology

Use:

* modern .NET
* Avalonia for cross-platform UI
* SkiaSharp for editor rendering
* ImageSharp where appropriate for image encoding and metadata
* Apple ScreenCaptureKit for macOS screenshot capture
* a small native macOS bridge only where required

Avoid introducing into new cross-platform code:

* System.Windows.Forms
* System.Drawing
* Win32 APIs
* Dapplo.Windows APIs
* Windows-only dependencies

## Architecture

Platform-independent functionality should be separated from platform-specific functionality.

Likely new projects may include:

* Greenshot.Core
* Greenshot.Imaging
* Greenshot.Editor.Core
* Greenshot.UI.Avalonia
* Greenshot.Platform.Mac
* Greenshot.Mac

A native macOS bridge may be added if required for ScreenCaptureKit.

Do not create all projects merely because they are listed here. Add them only when justified by the implementation.

## Development approach

Work incrementally.

Do not attempt to port the entire Greenshot application at once.

Before changing an existing Greenshot implementation, inspect the upstream code and determine whether:

1. it can be reused unchanged,
2. its algorithm can be reused with a different platform implementation,
3. it should remain Windows-specific,
4. or a new abstraction should be introduced.

Keep abstractions small and practical.

## First milestone

Create the smallest working native macOS proof of concept.

The initial target is:

1. macOS application starts successfully on Apple Silicon
2. screenshot capture can be triggered
3. ScreenCaptureKit captures an image
4. captured image is displayed in a window
5. image can be copied to the clipboard
6. image can be saved as PNG

Do not implement the full Greenshot editor during this milestone.

Do not implement advanced plugins during this milestone.

Region selection and global shortcuts should be added incrementally after basic capture works.

## Git rules

Keep changes small and logically grouped.

Do not rewrite Git history.

Do not commit:

* API keys
* certificates
* provisioning profiles
* secrets
* build output
* IDE caches

Do not push directly to `main`.

Development belongs on `macos-port` or feature branches based on it.

## Build and validation

After meaningful changes:

* build affected projects
* run available tests
* report build warnings and errors
* do not hide failing tests

If the existing Windows Greenshot solution cannot build on macOS, do not attempt large-scale modifications merely to make the Windows application build.

The original Windows implementation should remain intact unless a change is explicitly necessary for shared functionality.
