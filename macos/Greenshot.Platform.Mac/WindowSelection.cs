namespace Greenshot.Platform.Mac;

public readonly record struct WindowPoint(int X, int Y);
public readonly record struct WindowRect(int X, int Y, int Width, int Height)
{
    public bool Contains(WindowPoint point) =>
        point.X >= X && point.Y >= Y && point.X < X + Width && point.Y < Y + Height;

    public bool Intersects(WindowRect other) =>
        X < other.X + other.Width && other.X < X + Width &&
        Y < other.Y + other.Height && other.Y < Y + Height;
}

public sealed record SelectableWindow(
    uint WindowId,
    WindowRect Frame,
    string ApplicationName,
    string Title,
    int ZOrder,
    bool IsEligible = true);

public static class WindowHitTester
{
    public static SelectableWindow? HitTest(IEnumerable<SelectableWindow> windows, WindowPoint point)
    {
        return windows
            .Where(window => window.IsEligible && window.Frame.Contains(point))
            .OrderBy(window => window.ZOrder)
            .FirstOrDefault();
    }
}