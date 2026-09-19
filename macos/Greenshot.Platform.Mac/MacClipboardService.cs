using AppKit;
using Foundation;

namespace Greenshot.Platform.Mac;

public sealed class MacClipboardService : IClipboardService
{
    public Task CopyPngAsync(byte[] pngBytes)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);

        if (NSThread.IsMain)
        {
            CopyOnMainThread(pngBytes);
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        NSOperationQueue.MainQueue.AddOperation(() =>
        {
            try
            {
                CopyOnMainThread(pngBytes);
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });

        return completion.Task;
    }

    private static void CopyOnMainThread(byte[] pngBytes)
    {
        var pasteboard = NSPasteboard.GeneralPasteboard;
        pasteboard.ClearContents();

        using var data = NSData.FromArray(pngBytes);
        if (!pasteboard.SetDataForType(data, "public.png"))
        {
            throw new InvalidOperationException("macOS could not copy the PNG image to the clipboard.");
        }
    }
}