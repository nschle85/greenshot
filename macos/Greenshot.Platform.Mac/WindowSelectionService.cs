using AppKit;
using CoreGraphics;
using Foundation;
using ScreenCaptureKit;

namespace Greenshot.Platform.Mac;

public sealed class WindowSelectionService
{
    private readonly string? _ownBundleIdentifier = NSBundle.MainBundle.BundleIdentifier;
    private readonly Dictionary<uint, SCWindow> _nativeWindows = new();

    public async Task<IReadOnlyList<SelectableWindow>> GetEligibleWindowsAsync(
        WindowRect? displayBounds = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var content = await SCShareableContent.GetShareableContentAsync();
        var eligibleWindows = content.Windows
            .Select((window, index) => new { window, index })
            .Where(item => IsEligible(item.window))
            .Select(item => ToSelectableWindow(item.window, item.index))
            .Where(window => displayBounds is null || window.Frame.Intersects(displayBounds.Value))
            .ToArray();

        _nativeWindows.Clear();
        foreach (var window in content.Windows.Where(IsEligible))
        {
            _nativeWindows[window.WindowId] = window;
        }

        return eligibleWindows;
    }

    public async Task<CGImage> CaptureWindowAsync(
        SelectableWindow selectedWindow,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(selectedWindow);

        _nativeWindows.TryGetValue(selectedWindow.WindowId, out var window);
        if (window is null)
        {
            var content = await SCShareableContent.GetShareableContentAsync();
            window = content.Windows.FirstOrDefault(candidate => candidate.WindowId == selectedWindow.WindowId)
                ?? throw new InvalidOperationException("The selected window is no longer available.");
        }

        if (!IsEligible(window))
        {
            throw new InvalidOperationException("The selected window is no longer eligible for capture.");
        }

        var filter = new SCContentFilter(window);
        var contentRect = filter.ContentRect;
        var pixelScale = filter.PointPixelScale > 0 ? filter.PointPixelScale : 1;
        var configuration = new SCStreamConfiguration
        {
            Width = (nuint)Math.Max(1, Math.Round(contentRect.Width * pixelScale)),
            Height = (nuint)Math.Max(1, Math.Round(contentRect.Height * pixelScale)),
            ShowsCursor = false,
            CapturesAudio = false
        };
        return await SCScreenshotManager.CaptureImageAsync(filter, configuration);
    }

    private bool IsEligible(SCWindow window)
    {
        var bundleIdentifier = window.OwningApplication?.BundleIdentifier;
        var applicationName = window.OwningApplication?.ApplicationName;
        return window.Frame.Width > 0 && window.Frame.Height > 0 &&
               !string.IsNullOrWhiteSpace(applicationName) &&
               !string.Equals(bundleIdentifier, _ownBundleIdentifier, StringComparison.Ordinal) &&
               !string.Equals(bundleIdentifier, "com.apple.dock", StringComparison.Ordinal) &&
               !string.Equals(bundleIdentifier, "com.apple.WindowServer", StringComparison.Ordinal) &&
               window.WindowLayer == 0;
    }

    private static SelectableWindow ToSelectableWindow(SCWindow window, int zOrder)
    {
        var frame = window.Frame;
        return new SelectableWindow(
            window.WindowId,
            new WindowRect(
                checked((int)Math.Round(frame.X)),
                checked((int)Math.Round(frame.Y)),
                checked((int)Math.Round(frame.Width)),
                checked((int)Math.Round(frame.Height))),
            window.OwningApplication?.ApplicationName ?? "Unknown application",
            window.Title ?? string.Empty,
            zOrder);
    }
}