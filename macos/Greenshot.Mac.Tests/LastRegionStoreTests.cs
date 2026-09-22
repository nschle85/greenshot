using Avalonia;
using Xunit;

namespace Greenshot.Mac.Tests;

public sealed class LastRegionStoreTests
{
    [Fact]
    public void EmptyStoreHasNoRegion()
    {
        var store = new LastRegionStore();

        Assert.False(store.HasRegion);
        Assert.False(store.TryGetRegion(1920, 1080, out _));
    }

    [Fact]
    public void RememberedRegionIsReturnedForTheOriginalImageSize()
    {
        var store = new LastRegionStore();
        store.Remember(new PixelRect(100, 50, 800, 400), 1920, 1080);

        Assert.True(store.TryGetRegion(1920, 1080, out var result));
        Assert.Equal(new PixelRect(100, 50, 800, 400), result);
    }

    [Fact]
    public void RememberedRegionScalesWhenDisplayResolutionChanges()
    {
        var store = new LastRegionStore();
        store.Remember(new PixelRect(100, 50, 800, 400), 1920, 1080);

        Assert.True(store.TryGetRegion(3840, 2160, out var result));
        Assert.Equal(new PixelRect(200, 100, 1600, 800), result);
    }

    [Fact]
    public void InvalidRegionIsNotStored()
    {
        var store = new LastRegionStore();

        Assert.Throws<ArgumentException>(() => store.Remember(new PixelRect(900, 500, 100, 100), 960, 600));
        Assert.False(store.HasRegion);
    }

    [Fact]
    public void RescaledRegionIsClampedAndInvalidCurrentImageIsRejected()
    {
        var store = new LastRegionStore();
        store.Remember(new PixelRect(800, 400, 160, 100), 960, 600);

        Assert.True(store.TryGetRegion(480, 300, out var result));
        Assert.Equal(new PixelRect(400, 200, 80, 50), result);
        Assert.False(store.TryGetRegion(0, 300, out _));
    }
}