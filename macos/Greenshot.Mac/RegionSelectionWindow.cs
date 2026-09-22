using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Greenshot.Platform.Mac;

namespace Greenshot.Mac;

internal sealed class RegionSelectionWindow : Window
{
    private readonly SelectionCanvas _canvas;
    private readonly TaskCompletionSource<PixelRect?> _selection = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private RegionSelectionWindow(Bitmap screenshot, PixelRect screenBounds, double scaling)
    {
        SystemDecorations = SystemDecorations.None;
        CanResize = false;
        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowState = WindowState.Normal;
        Position = screenBounds.Position;
        Width = screenBounds.Width / scaling;
        Height = screenBounds.Height / scaling;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;

        _canvas = new SelectionCanvas(screenshot, screenshot.PixelSize.Width, screenshot.PixelSize.Height);
        Content = _canvas;
        KeyDown += OnKeyDown;
        Opened += (_, _) => Dispatcher.UIThread.Post(ConfigureDesktopOverlay, DispatcherPriority.Loaded);
        Closed += (_, _) => _selection.TrySetResult(null);
    }

    public static async Task<PixelRect?> SelectAsync(Window owner, Bitmap screenshot, PixelRect screenBounds, double scaling)
    {
        var window = new RegionSelectionWindow(screenshot, screenBounds, scaling);
        // The owner is hidden while the overlay is active. Showing this as an
        // owned window causes macOS to immediately hide/close it with its owner.
        // Keep the overlay as an independent top-level window instead.
        window.Show();
        window.Activate();
        return await window._selection.Task;
    }

    private void ConfigureDesktopOverlay()
    {
        try
        {
            if (TryGetPlatformHandle() is { HandleDescriptor: "NSWindow" } handle)
            {
                DesktopOverlayWindowService.Configure(handle.Handle);
            }

            Activate();
        }
        catch (Exception exception)
        {
            _selection.TrySetException(exception);
            Close();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        _selection.TrySetResult(null);
        Close();
    }

    private sealed class SelectionCanvas : Control
    {
        private readonly Bitmap _screenshot;
        private readonly int _imageWidth;
        private readonly int _imageHeight;
        private Point? _start;
        private Point? _current;
        private bool _dragging;

        public SelectionCanvas(Bitmap screenshot, int imageWidth, int imageHeight)
        {
            _screenshot = screenshot;
            _imageWidth = imageWidth;
            _imageHeight = imageHeight;
            Focusable = true;
            Cursor = new Cursor(StandardCursorType.Cross);
        }


        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Focus();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            context.DrawImage(_screenshot, new Rect(Bounds.Size));

            if (_start is not { } start || _current is not { } current)
            {
                return;
            }

            var selection = new Rect(start, current).Normalize();
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(125, 0, 0, 0)), null, new Rect(Bounds.Size));
            context.DrawRectangle(null, new Pen(Brushes.White, 2), selection);
            context.DrawRectangle(null, new Pen(Brushes.Black, 1), selection.Deflate(1));

            var pixels = SelectionCoordinateMapper.ToImagePixels(start, current, Bounds.Size, _imageWidth, _imageHeight);
            var label = new FormattedText(
                $"{pixels.Width} × {pixels.Height}",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                14,
                Brushes.White);
            var labelRect = new Rect(selection.X, Math.Max(0, selection.Y - label.Height - 8), label.Width + 12, label.Height + 6);
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(220, 0, 0, 0)), null, labelRect);
            context.DrawText(label, labelRect.Position + new Vector(6, 3));
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!e.Handled && e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
            {
                _start = _current = e.GetPosition(this);
                _dragging = true;
                e.Pointer.Capture(this);
                InvalidateVisual();
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (_dragging)
            {
                _current = e.GetPosition(this);
                InvalidateVisual();
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (!_dragging || _start is not { } start)
            {
                return;
            }

            _current = e.GetPosition(this);
            _dragging = false;
            e.Pointer.Capture(null);
            var pixels = SelectionCoordinateMapper.ToImagePixels(start, _current.Value, Bounds.Size, _imageWidth, _imageHeight);
            if (pixels.Width > 0 && pixels.Height > 0 && VisualRoot is RegionSelectionWindow window)
            {
                window._selection.TrySetResult(pixels);
                window.Close();
            }
            else
            {
                InvalidateVisual();
            }
        }
    }
}