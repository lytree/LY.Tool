using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.LayoutDisplay.Pages;

namespace LYBox.Plugin.LayoutDisplay.ViewModels;

[NavigationItem("KeyImageViewer")]
[Menu("NAV_ImageViewer", "KeyImageViewer", "NAV_LayoutDisplay")]
[ViewMap(typeof(ImageViewerDemo))]
public partial class ImageViewerDemoViewModel : ViewModelBase
{
    [ObservableProperty] private IImage? _source;
    [ObservableProperty] private double _zoom = 1.0;
    [ObservableProperty] private double _offsetX;
    [ObservableProperty] private double _offsetY;

    public ImageViewerDemoViewModel()
    {
        Source = CreateDemoImage();
    }

    [RelayCommand]
    private async Task OpenFileAsync(IReadOnlyList<IStorageItem>? items)
    {
        if (items is null || items.Count == 0 || items[0] is not IStorageFile file) return;
        await using var stream = await file.OpenReadAsync();
        Source = new Bitmap(stream);
    }

    [RelayCommand]
    private void ResetImage()
    {
        Source = CreateDemoImage();
    }

    [RelayCommand]
    private void ResetView()
    {
        Zoom = 1.0;
        OffsetX = 0;
        OffsetY = 0;
    }

    private static WriteableBitmap CreateDemoImage()
    {
        const int width = 600;
        const int height = 400;
        var bmp = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Rgba8888);

        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var r = (byte)(255 * x / width);
                var g = (byte)(128 + 127 * y / height);
                var b = (byte)(255 * (width - x) / width);
                var a = (byte)255;

                // Draw a subtle checkerboard overlay so zoom/pixel-level panning is visible.
                if (((x / 16) + (y / 16)) % 2 == 0)
                {
                    r = (byte)(r * 0.85);
                    g = (byte)(g * 0.85);
                    b = (byte)(b * 0.85);
                }

                // Draw a crosshair at the centre.
                var cx = width / 2;
                var cy = height / 2;
                if ((x >= cx - 2 && x <= cx + 2) || (y >= cy - 2 && y <= cy + 2))
                {
                    r = 0;
                    g = 0;
                    b = 0;
                }

                // Draw border.
                if (x < 1 || x >= width - 1 || y < 1 || y >= height - 1)
                {
                    r = 40;
                    g = 40;
                    b = 40;
                }

                var index = (y * width + x) * 4;
                pixels[index] = r;
                pixels[index + 1] = g;
                pixels[index + 2] = b;
                pixels[index + 3] = a;
            }
        }

        using var fb = bmp.Lock();
        Marshal.Copy(pixels, 0, fb.Address, pixels.Length);
        return bmp;
    }
}
