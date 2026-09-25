using Greenshot.Platform.Mac;
using Xunit;

namespace Greenshot.Mac.Tests;

public sealed class WindowHitTesterTests
{
    [Fact]
    public void HitTestReturnsFrontmostEligibleWindow()
    {
        var windows = new[]
        {
            new SelectableWindow(1, new WindowRect(0, 0, 500, 500), "Back", "", 1),
            new SelectableWindow(2, new WindowRect(100, 100, 300, 300), "Front", "", 0)
        };

        var result = WindowHitTester.HitTest(windows, new WindowPoint(200, 200));

        Assert.NotNull(result);
        Assert.Equal((uint)2, result.WindowId);
    }

    [Fact]
    public void HitTestIgnoresIneligibleAndOutsideWindows()
    {
        var windows = new[]
        {
            new SelectableWindow(1, new WindowRect(0, 0, 100, 100), "Hidden", "", 0, false),
            new SelectableWindow(2, new WindowRect(200, 200, 100, 100), "Other", "", 0)
        };

        Assert.Null(WindowHitTester.HitTest(windows, new WindowPoint(50, 50)));
        Assert.Null(WindowHitTester.HitTest(windows, new WindowPoint(150, 150)));
    }

    [Fact]
    public void HitTestUsesHalfOpenWindowEdges()
    {
        var windows = new[]
        {
            new SelectableWindow(1, new WindowRect(10, 20, 100, 80), "App", "Window", 0)
        };

        Assert.NotNull(WindowHitTester.HitTest(windows, new WindowPoint(10, 20)));
        Assert.Null(WindowHitTester.HitTest(windows, new WindowPoint(110, 100)));
    }
}