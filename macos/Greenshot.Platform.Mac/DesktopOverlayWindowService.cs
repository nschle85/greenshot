using AppKit;
using CoreGraphics;
using ObjCRuntime;

namespace Greenshot.Platform.Mac;

public static class DesktopOverlayWindowService
{
    public static void Configure(IntPtr nativeWindowHandle)
    {
        var previousThreadCheck = NSApplication.CheckForIllegalCrossThreadCalls;

        // Avalonia.Native owns the Cocoa event loop but initializes it without
        // Xamarin.Mac's NSApplication.Main. Its managed-thread check therefore
        // rejects calls from Avalonia's native UI dispatcher even though this is
        // the thread that owns the NSWindow. Keep the override tightly scoped.
        NSApplication.CheckForIllegalCrossThreadCalls = false;
        try
        {
            if (Runtime.GetNSObject<NSWindow>(nativeWindowHandle) is not { } window ||
                (window.Screen ?? NSScreen.MainScreen) is not { } screen)
            {
                return;
            }

            var displayFrame = screen.Frame;
            window.StyleMask = NSWindowStyle.Borderless;
            window.Level = NSWindowLevel.ScreenSaver;
            window.CollectionBehavior = NSWindowCollectionBehavior.CanJoinAllSpaces |
                                        NSWindowCollectionBehavior.Stationary |
                                        NSWindowCollectionBehavior.FullScreenAuxiliary;
            window.SetFrame(displayFrame, true);
            window.SetContentSize(displayFrame.Size);

            if (window.ContentView is { } contentView)
            {
                contentView.AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable;
                contentView.Frame = new CGRect(CGPoint.Empty, displayFrame.Size);
            }

            window.MakeKeyAndOrderFront(null);
        }
        finally
        {
            NSApplication.CheckForIllegalCrossThreadCalls = previousThreadCheck;
        }
    }
}