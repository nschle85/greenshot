using AppKit;

namespace Greenshot.Platform.Mac;

public static class MacApplicationActivationService
{
    public static void Activate()
    {
        var previousThreadCheck = NSApplication.CheckForIllegalCrossThreadCalls;
        NSApplication.CheckForIllegalCrossThreadCalls = false;
        try
        {
            NSApplication.SharedApplication.Activate();
        }
        finally
        {
            NSApplication.CheckForIllegalCrossThreadCalls = previousThreadCheck;
        }
    }
}