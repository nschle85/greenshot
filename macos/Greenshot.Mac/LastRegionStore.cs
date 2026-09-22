using Avalonia;

namespace Greenshot.Mac;

internal sealed class LastRegionStore
{
    private PixelRect? _region;
    private PixelSize _sourceImageSize;

    public bool HasRegion => _region is not null;

    public void Remember(PixelRect region, int sourceImageWidth, int sourceImageHeight)
    {
        if (!IsValid(region, sourceImageWidth, sourceImageHeight))
        {
            throw new ArgumentException("The remembered region must be non-empty and within the source image.", nameof(region));
        }

        _region = region;
        _sourceImageSize = new PixelSize(sourceImageWidth, sourceImageHeight);
    }

    public bool TryGetRegion(int imageWidth, int imageHeight, out PixelRect region)
    {
        region = default;
        if (_region is not { } stored || imageWidth <= 0 || imageHeight <= 0 ||
            _sourceImageSize.Width <= 0 || _sourceImageSize.Height <= 0)
        {
            return false;
        }

        var left = (int)Math.Floor((double)stored.X / _sourceImageSize.Width * imageWidth);
        var top = (int)Math.Floor((double)stored.Y / _sourceImageSize.Height * imageHeight);
        var right = (int)Math.Ceiling((double)(stored.X + stored.Width) / _sourceImageSize.Width * imageWidth);
        var bottom = (int)Math.Ceiling((double)(stored.Y + stored.Height) / _sourceImageSize.Height * imageHeight);

        var x = Clamp(left, 0, imageWidth);
        var y = Clamp(top, 0, imageHeight);
        var clampedRight = Clamp(right, 0, imageWidth);
        var clampedBottom = Clamp(bottom, 0, imageHeight);
        region = new PixelRect(x, y, clampedRight - x, clampedBottom - y);
        return region.Width > 0 && region.Height > 0;
    }

    private static bool IsValid(PixelRect region, int imageWidth, int imageHeight) =>
        imageWidth > 0 && imageHeight > 0 && region.X >= 0 && region.Y >= 0 &&
        region.Width > 0 && region.Height > 0 &&
        region.X <= imageWidth - region.Width && region.Y <= imageHeight - region.Height;

    private static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);
}