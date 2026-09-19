namespace Greenshot.Platform.Mac;

public interface IClipboardService
{
    Task CopyPngAsync(byte[] pngBytes);
}