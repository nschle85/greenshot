using System.IO;
using CoreGraphics;
using Foundation;
using ImageIO;
using ScreenCaptureKit;
using UniformTypeIdentifiers;

namespace Greenshot.Platform.Mac;

public sealed class ScreenCaptureService
{
    public async Task<CGImage> CapturePrimaryDisplayAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var content = await SCShareableContent.GetShareableContentAsync();
        var display = content.Displays.FirstOrDefault()
            ?? throw new InvalidOperationException("No display is available for capture.");

        var filter = new SCContentFilter(display, Array.Empty<SCWindow>(), SCContentFilterOption.Exclude);
        var configuration = new SCStreamConfiguration
        {
            Width = (nuint)display.Width,
            Height = (nuint)display.Height,
            ShowsCursor = false
        };

        return await SCScreenshotManager.CaptureImageAsync(filter, configuration);
    }

    public async Task<byte[]> CapturePrimaryDisplayAsPngBytesAsync(CancellationToken cancellationToken = default)
    {
        using var cgImage = await CapturePrimaryDisplayAsync(cancellationToken);
        using var data = new NSMutableData();
        using (var destination = CGImageDestination.Create(data, UTTypes.Png.Identifier, 1))
        {
            if (destination == null)
            {
                throw new InvalidOperationException("Failed to create CGImageDestination for PNG encoding.");
            }

            destination.AddImage(cgImage);
            if (!destination.Close())
            {
                throw new InvalidOperationException("Failed to finalize PNG image destination.");
            }
        }

        return data.ToArray();
    }
}
