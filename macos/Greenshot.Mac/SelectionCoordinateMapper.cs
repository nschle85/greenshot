using Avalonia;

namespace Greenshot.Mac;

internal static class SelectionCoordinateMapper
{
    public static PixelRect ToImagePixels(Point start, Point end, Size overlaySize, int imageWidth, int imageHeight)
    {
        if (overlaySize.Width <= 0 || overlaySize.Height <= 0 || imageWidth <= 0 || imageHeight <= 0)
        {
            return default;
        }

        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var right = Math.Max(start.X, end.X);
        var bottom = Math.Max(start.Y, end.Y);

        if (right <= left || bottom <= top)
        {
            return default;
        }

        var x = Clamp((int)Math.Floor(left / overlaySize.Width * imageWidth), 0, imageWidth);
        var y = Clamp((int)Math.Floor(top / overlaySize.Height * imageHeight), 0, imageHeight);
        var rightPixel = Clamp((int)Math.Ceiling(right / overlaySize.Width * imageWidth), 0, imageWidth);
        var bottomPixel = Clamp((int)Math.Ceiling(bottom / overlaySize.Height * imageHeight), 0, imageHeight);

        return new PixelRect(x, y, rightPixel - x, bottomPixel - y);
    }

    private static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);
}