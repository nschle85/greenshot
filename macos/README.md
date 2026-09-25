# Greenshot for macOS

## Setup

### Prerequisites

- macOS on Apple Silicon (`osx-arm64`)
- .NET SDK 9
- JetBrains Rider (optional, for IDE-based development)

### 1. Configure the .NET SDK in Rider

Open `Greenshot.Mac.sln` in Rider and go to **Rider > Settings/Preferences > Build, Execution, Deployment > Toolset and Build**. Ensure the .NET CLI path points to the installed `dotnet` executable, for example:

- `/usr/local/share/dotnet/dotnet`
- `$HOME/.dotnet/dotnet`

### 2. Install the macOS workload

The projects target `net9.0-macos15.0` and require the .NET macOS workload. From the project root, run:

```bash
dotnet workload restore
```

If the workload is not available yet, install it explicitly:

```bash
sudo dotnet workload install macos --skip-manifest-update
```

### 3. Restore and build

```bash
dotnet restore Greenshot.Mac.sln
dotnet build Greenshot.Mac.sln --no-restore
```

A successful build produces the application under:

```text
Greenshot.Mac/bin/Debug/net9.0-macos15.0/osx-arm64/
```

### 4. Run the application

Run the `Greenshot.Mac` project from Rider, or launch the generated executable from the project root:

```bash
./Greenshot.Mac/bin/Debug/net9.0-macos15.0/osx-arm64/Greenshot.Mac.app/Contents/MacOS/Greenshot.Mac
```

### Project configuration notes

- Target framework: `net9.0-macos15.0`
- Minimum supported macOS version: `14.0`
- Runtime identifier: `osx-arm64`
- Application ID: `org.greenshot.mac`

### Region-selection fullscreen overlay

- The region selector is a standalone, borderless Avalonia window. The main window is hidden before capture and the overlay is intentionally shown without an owner, because an owned window is hidden with its owner on macOS.
- The initial Avalonia position and dimensions use `Screens.Primary.Bounds` and the runtime display scale. `Screen.WorkingArea`, maximized state, and native fullscreen Spaces must not replace this setup: they leave the menu-bar or Dock area outside Avalonia's interactive surface.
- After opening, `DesktopOverlayWindowService` configures the existing native `NSWindow` using `NSScreen.Frame`, `NSWindowLevel.ScreenSaver`, and the required Spaces collection behavior. It also resizes the hosted `ContentView`; changing only the outer native frame regresses the fullscreen overlay by leaving Avalonia's render and input surface at the smaller visible work area. This AppKit workaround is required for coverage behind the menu bar and Dock and must remain isolated in `Greenshot.Platform.Mac`.
- The native service temporarily disables `NSApplication.CheckForIllegalCrossThreadCalls` only while Avalonia's Cocoa UI dispatcher mutates the `NSWindow`, and restores its previous value in `finally`. The setting is process-wide, so the service must remain UI-dispatcher-only and must not be invoked concurrently.
- The 100 ms delay after hiding the main window is deliberate. It gives AppKit time to commit the hide before ScreenCaptureKit freezes the desktop, preventing the main window's Liquid Glass surface from appearing in the selection background. Do not remove it without a verified compositor-synchronization replacement.
- The overlay bitmap is only a frozen display preview. `SelectionCoordinateMapper` maps its logical coordinates to native screenshot pixels, and `ScreenCaptureService.CropToPngBytes` crops the original `CGImage`. Copy and Save PNG therefore operate on the full-resolution cropped PNG bytes, not the scaled preview.

### Menu bar and global shortcuts

- `App` installs an Avalonia `TrayIcon` with `NativeMenu` commands for `Capture Region`, `Capture Screen`, `Capture Window`, `Capture Last Region`, `Open Greenshot`, and `Quit Greenshot`. The tray uses a generated neutral icon so no platform asset is required.
- The desktop lifetime uses `ShutdownMode.OnExplicitShutdown`; closing the main window hides it while the tray icon and shortcuts remain active. `Quit Greenshot` explicitly shuts down and disposes the tray and hotkey services.
- `GlobalHotKeyService` uses macOS Carbon `RegisterEventHotKey` rather than global keyboard monitoring. The default configurable bindings are Command+Shift+R for region capture, Command+Shift+S for screen capture, Command+Shift+L for last-region capture, and Command+Shift+W for window capture; registration conflicts are reported without preventing normal menu/button use.
- Hotkey callbacks are posted to Avalonia's UI dispatcher and share the same capture gate as the window and tray commands, so repeated presses cannot overlap captures. The existing hide, delay, fullscreen overlay and Escape behavior is unchanged.
- Screen and region capture hide the main window before capturing, then update the preview before showing and activating the existing window. `MacApplicationActivationService` requests macOS foreground activation after capture without using permanent `Topmost` state.
- `LastRegionStore` remembers the last successfully selected native pixel rectangle for the current session. `Capture Last Region` stays disabled until a selection succeeds; if the display resolution changes, the rectangle is scaled to the new captured image dimensions and clamped before cropping. Cancelling selection never changes the stored region.
- `Capture Window` uses a separate fullscreen Avalonia overlay with the same `DesktopOverlayWindowService` AppKit workaround as region selection. `WindowSelectionService` enumerates eligible on-screen `SCWindow` instances, preserves ScreenCaptureKit's documented front-to-back order for hit-testing, and captures the selected window directly with an `SCContentFilter` rather than cropping the desktop image. The initial implementation prioritizes the primary display; minimized windows, other displays and native fullscreen Spaces remain platform limitations to verify manually.
- The configurable default global shortcut for window capture is Command+Shift+W. Escape cancels window selection without changing the previous screenshot.

### Rider source mapping

The macOS project may require access to the original Greenshot source files. In Rider, add the original source directory as an additional source or content root:

```text
$PROJECT_DIR$/../src
```

This is a local Rider workspace setting and should not be committed with the project files.
