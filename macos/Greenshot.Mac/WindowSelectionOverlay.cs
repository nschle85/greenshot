using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Greenshot.Platform.Mac;

namespace Greenshot.Mac;

internal sealed class WindowSelectionOverlay : Window
{
    private readonly TaskCompletionSource<SelectableWindow?> _selection =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private WindowSelectionOverlay(
        Bitmap screenshot,
        PixelRect screenBounds,
        double scaling,
        IReadOnlyList<SelectableWindow> windows)
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

        Content = new SelectionCanvas(screenshot, screenBounds, scaling, windows, this);
        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                args.Handled = true;
                _selection.TrySetResult(null);
                Close();
            }
        };
        Opened += (_, _) => Dispatcher.UIThread.Post(ConfigureOverlay, DispatcherPriority.Loaded);
        Closed += (_, _) => _selection.TrySetResult(null);
    }

    public static async Task<SelectableWindow?> SelectAsync(
        Bitmap screenshot,
        PixelRect screenBounds,
        double scaling,
        IReadOnlyList<SelectableWindow> windows)
    {
        var overlay = new WindowSelectionOverlay(screenshot, screenBounds, scaling, windows);
        overlay.Show();
        overlay.Activate();
        return await overlay._selection.Task;
    }

    private void ConfigureOverlay()
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

    private sealed class SelectionCanvas : Control
    {
        private readonly Bitmap _screenshot;
        private readonly PixelRect _screenBounds;
        private readonly double _scaling;
        private readonly IReadOnlyList<SelectableWindow> _windows;
        private readonly WindowSelectionOverlay _owner;
        private SelectableWindow? _highlighted;

        public SelectionCanvas(
            Bitmap screenshot,
            PixelRect screenBounds,
            double scaling,
            IReadOnlyList<SelectableWindow> windows,
            WindowSelectionOverlay owner)
        {
            _screenshot = screenshot;
            _screenBounds = screenBounds;
            _scaling = scaling;
            _windows = windows;
            _owner = owner;
            Focusable = true;
            Cursor = new Cursor(StandardCursorType.Arrow);
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
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(105, 0, 0, 0)), null, new Rect(Bounds.Size));

            if (_highlighted is not { } selected)
            {
                return;
            }

            var bounds = ToOverlayRect(selected.Frame);
            using (context.PushClip(bounds))
            {
                context.DrawImage(_screenshot, new Rect(Bounds.Size));
            }

            context.DrawRectangle(null, new Pen(Brushes.White, 3), bounds);
            context.DrawRectangle(null, new Pen(Brushes.Black, 1), bounds.Deflate(2));

            var title = string.IsNullOrWhiteSpace(selected.Title)
                ? selected.ApplicationName
                : $"{selected.ApplicationName} — {selected.Title}";
            var label = new FormattedText(
                title,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                14,
                Brushes.White);
            var labelRect = new Rect(bounds.X, Math.Max(0, bounds.Y - label.Height - 8), label.Width + 12, label.Height + 6);
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(220, 0, 0, 0)), null, labelRect);
            context.DrawText(label, labelRect.Position + new Vector(6, 3));
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            var position = e.GetPosition(this);
            var point = ToScreenPoint(position);
            var highlighted = WindowHitTester.HitTest(_windows, point);
            if (highlighted?.WindowId != _highlighted?.WindowId)
            {
                _highlighted = highlighted;
                InvalidateVisual();
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (_highlighted is not null &&
                e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
            {
                _owner._selection.TrySetResult(_highlighted);
                _owner.Close();
                e.Handled = true;
            }
        }

        private WindowPoint ToScreenPoint(Point point) => new(
            checked((int)Math.Round(_screenBounds.X + point.X * _scaling)),
            checked((int)Math.Round(_screenBounds.Y + point.Y * _scaling)));

        private Rect ToOverlayRect(WindowRect frame) => new(
            (frame.X - _screenBounds.X) / _scaling,
            (frame.Y - _screenBounds.Y) / _scaling,
            frame.Width / _scaling,
            frame.Height / _scaling);
    }
}