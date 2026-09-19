using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
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
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
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
    private byte[]? _capturedPngBytes;

    public MainWindow()
    {
        Title = "Greenshot macOS Proof of Concept";
        Width = 800;
        Height = 600;

        var button = new Button { Content = "Capture Screen", HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += CaptureClicked;
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
                    Children = { button, _copyButton, _saveButton, _status }
                },
                _image
            }
        };
        DockPanel.SetDock(Content is DockPanel panel ? panel.Children[0] : button, Dock.Top);
    }

    private async void CaptureClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            _status.Text = "Capturing… Grant Screen Recording access if macOS asks.";
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
