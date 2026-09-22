using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Greenshot.Platform.Mac;

namespace Greenshot.Mac;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}

internal sealed class App : Application
{
    private GlobalHotKeyService? _hotKeys;
    private TrayIcon? _trayIcon;
    private WindowIcon? _trayIconImage;
    private bool _isQuitting;

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;
            ConfigureTray(mainWindow, desktop);
            ConfigureHotKeys(mainWindow);
            desktop.Exit += (_, _) => DisposePlatformResources();
            mainWindow.Closing += (_, args) =>
            {
                if (!_isQuitting)
                {
                    args.Cancel = true;
                    mainWindow.Hide();
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureTray(MainWindow mainWindow, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var menu = new NativeMenu();
        menu.Items.Add(CreateMenuItem("Capture Region", () => mainWindow.CaptureRegionAsync()));
        menu.Items.Add(CreateMenuItem("Capture Screen", () => mainWindow.CaptureScreenAsync()));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(CreateMenuItem("Open Greenshot", () =>
        {
            mainWindow.Show();
            mainWindow.Activate();
        }));
        menu.Items.Add(CreateMenuItem("Quit Greenshot", () =>
        {
            _isQuitting = true;
            desktop.Shutdown();
        }));

        _trayIcon = new TrayIcon
        {
            IsVisible = true,
            ToolTipText = "Greenshot",
            Icon = _trayIconImage = CreateTrayIcon(),
            Menu = menu
        };
        SetValue(TrayIcon.IconsProperty, new TrayIcons { _trayIcon });
    }

    private static NativeMenuItem CreateMenuItem(string header, Action action)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => action();
        return item;
    }

    private void ConfigureHotKeys(MainWindow mainWindow)
    {
        _hotKeys = new GlobalHotKeyService(GlobalHotKeySettings.Default);
        _hotKeys.RegistrationFailed += (_, exception) =>
        {
            Trace.WriteLine($"Global shortcut registration failed: {exception.Message}");
            mainWindow.SetStatus("Global shortcuts unavailable; use the menu bar or window buttons.");
        };
        _hotKeys.HotKeyPressed += (_, hotKey) => Dispatcher.UIThread.Post(() =>
        {
            _ = hotKey == GlobalHotKeyAction.CaptureRegion
                ? mainWindow.CaptureRegionAsync()
                : mainWindow.CaptureScreenAsync();
        });
        _hotKeys.Start();
    }

    private void DisposePlatformResources()
    {
        _hotKeys?.Dispose();
        _trayIcon?.Dispose();
        _hotKeys = null;
        _trayIcon = null;
        _trayIconImage = null;
    }

    private static WindowIcon CreateTrayIcon()
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(16, 16),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
        var pixels = new byte[16 * 16 * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 48;
            pixels[index + 1] = 160;
            pixels[index + 2] = 80;
            pixels[index + 3] = 255;
        }

        using (var framebuffer = bitmap.Lock())
        {
            Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);
        }

        return new WindowIcon(bitmap);
    }
}

internal sealed class MainWindow : Window
{
    private readonly Image _image = new() { Stretch = Avalonia.Media.Stretch.Uniform };
    private readonly TextBlock _status = new();
    private readonly ScreenCaptureService _capture = new();
    private readonly IClipboardService _clipboard = new MacClipboardService();
    private readonly Button _copyButton;
    private readonly Button _saveButton;
    private readonly SemaphoreSlim _captureGate = new(1, 1);
    private byte[]? _capturedPngBytes;
    private Bitmap? _overlayBitmap;

    public MainWindow()
    {
        Title = "Greenshot macOS Proof of Concept";
        Width = 800;
        Height = 600;

        var button = new Button { Content = "Capture Screen", HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += (_, _) => _ = CaptureScreenAsync();
        var regionButton = new Button { Content = "Capture Region", HorizontalAlignment = HorizontalAlignment.Left };
        regionButton.Click += (_, _) => _ = CaptureRegionAsync();
        _copyButton = new Button
        {
            Content = "Copy",
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = false
        };
        _copyButton.Click += CopyClicked;
        _saveButton = new Button
        {
            Content = "Save PNG",
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = false
        };
        _saveButton.Click += SaveClicked;
        Content = new DockPanel
        {
            Margin = new Avalonia.Thickness(12),
            Children =
            {
                new StackPanel
                {
                    Spacing = 8,
                    Orientation = Orientation.Horizontal,
                    Children = { button, regionButton, _copyButton, _saveButton, _status }
                },
                _image
            }
        };
        DockPanel.SetDock(Content is DockPanel panel ? panel.Children[0] : button, Dock.Top);
    }

    internal void SetStatus(string message) => _status.Text = message;

    public Task CaptureScreenAsync() => RunCaptureAsync(CaptureScreenCoreAsync);

    public Task CaptureRegionAsync() => RunCaptureAsync(CaptureRegionCoreAsync);

    private async Task RunCaptureAsync(Func<Task> capture)
    {
        if (!await _captureGate.WaitAsync(0))
        {
            _status.Text = "A capture is already in progress.";
            return;
        }

        try
        {
            await capture();
        }
        finally
        {
            _captureGate.Release();
        }
    }

    private async Task CaptureScreenCoreAsync()
    {
        try
        {
            _status.Text = "Capturing… Grant Screen Recording access if macOS asks.";

            Hide();
            await Task.Yield();
            await Task.Delay(100);

            var pngBytes = await _capture.CapturePrimaryDisplayAsPngBytesAsync();
            _capturedPngBytes = pngBytes;
            using var stream = new MemoryStream(pngBytes, writable: false);
            _image.Source = new Bitmap(stream);
            _copyButton.IsEnabled = true;
            _saveButton.IsEnabled = true;
            _status.Text = "Captured primary display.";
        }
        catch (Exception exception)
        {
            _status.Text = exception.Message;
        }
        finally
        {
            Show();
            Activate();
            MacApplicationActivationService.Activate();
        }
    }

    private async Task CaptureRegionCoreAsync()
    {
        var primaryScreen = Screens.Primary;
        if (primaryScreen is null)
        {
            _status.Text = "No primary display is available.";
            return;
        }

        try
        {
            _status.Text = "Capturing primary display…";
            Hide();

            // Let AppKit commit the hide before ScreenCaptureKit freezes the
            // desktop. Without this yield, macOS can include the main window's
            // Liquid Glass surface in the selection background.
            await Task.Yield();
            await Task.Delay(100);

            using var fullImage = await _capture.CapturePrimaryDisplayAsync();
            var fullPngBytes = ScreenCaptureService.EncodePng(fullImage);
            // Keep the overlay bitmap alive after SelectAsync completes. Closing the
            // overlay completes its task before Avalonia has necessarily finished
            // its final render pass, so disposing it in this method can make that
            // render pass dereference a disposed native bitmap.
            _overlayBitmap?.Dispose();
            _overlayBitmap = new Bitmap(new MemoryStream(fullPngBytes, writable: false));
            var pixels = await RegionSelectionWindow.SelectAsync(
                this,
                _overlayBitmap,
                primaryScreen.Bounds,
                primaryScreen.Scaling);

            if (pixels is null)
            {
                _status.Text = "Region capture cancelled.";
                return;
            }

            _capturedPngBytes = ScreenCaptureService.CropToPngBytes(
                fullImage,
                pixels.Value.X,
                pixels.Value.Y,
                pixels.Value.Width,
                pixels.Value.Height);
            ShowCapturedImage(_capturedPngBytes);
            _status.Text = $"Captured region ({pixels.Value.Width} × {pixels.Value.Height} px).";
        }
        catch (Exception exception)
        {
            _status.Text = exception.Message;
        }
        finally
        {
            Show();
            Activate();
            MacApplicationActivationService.Activate();
        }
    }

    private void ShowCapturedImage(byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes, writable: false);
        _image.Source = new Bitmap(stream);
        _copyButton.IsEnabled = true;
        _saveButton.IsEnabled = true;
    }

    private async void CopyClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_capturedPngBytes is null)
        {
            return;
        }

        try
        {
            await _clipboard.CopyPngAsync(_capturedPngBytes);
            _status.Text = "Copied screenshot to the macOS clipboard.";
        }
        catch (Exception exception)
        {
            _status.Text = $"Could not copy screenshot: {exception.Message}";
        }
    }

    private async void SaveClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_capturedPngBytes is null)
        {
            return;
        }

        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = $"Greenshot {DateTime.Now:yyyy-MM-dd HH.mm.ss}.png",
                DefaultExtension = "png",
                FileTypeChoices =
                [
                    new FilePickerFileType("PNG image")
                    {
                        Patterns = ["*.png"]
                    }
                ]
            });

            if (file is null)
            {
                _status.Text = "Save cancelled.";
                return;
            }

            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(_capturedPngBytes);
            _status.Text = "Saved screenshot as PNG.";
        }
        catch (Exception exception)
        {
            _status.Text = $"Could not save screenshot: {exception.Message}";
        }
    }
}
