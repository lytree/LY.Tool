using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SP = global::ScottPlot;

namespace Avalonia.Plugin.ScottPlot.Controls;

public class PlotMenu : SP.IPlotMenu
{
    public string DefaultSaveImageFilename { get; set; } = "Plot.png";
    public List<SP.ContextMenuItem> ContextMenuItems { get; set; } = [];
    private readonly PlotView _plotView;

    public PlotMenu(PlotView plotView)
    {
        _plotView = plotView;
        Reset();
    }

    public SP.ContextMenuItem[] GetDefaultContextMenuItems()
    {
        return
        [
            new() { Label = "Save Image", OnInvoke = OpenSaveImageDialog },
            new() { Label = "Copy to Clipboard", OnInvoke = CopyToClipboard },
            new() { Label = "Autoscale", OnInvoke = Autoscale },
        ];
    }

    public ContextMenu GetContextMenu(SP.Plot plot)
    {
        List<MenuItem> items = [];
        foreach (var contextMenuItem in ContextMenuItems)
        {
            if (contextMenuItem.IsSeparator)
            {
                items.Add(new MenuItem { Header = "-" });
            }
            else
            {
                var menuItem = new MenuItem { Header = contextMenuItem.Label };
                menuItem.Click += (s, e) => contextMenuItem.OnInvoke(plot);
                items.Add(menuItem);
            }
        }

        return new() { ItemsSource = items };
    }

    public async void OpenSaveImageDialog(SP.Plot plot)
    {
        var topLevel = TopLevel.GetTopLevel(_plotView)
                       ?? throw new NullReferenceException("Could not find a top level");

        var destinationFile = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
        {
            SuggestedFileName = DefaultSaveImageFilename,
            FileTypeChoices = FilePickerFileTypes
        });

        string? path = destinationFile?.TryGetLocalPath();
        if (path is not null && !string.IsNullOrWhiteSpace(path))
        {
            SP.PixelSize lastRenderSize = plot.RenderManager.LastRender.FigureRect.Size;
            plot.Save(path, (int)lastRenderSize.Width, (int)lastRenderSize.Height,
                SP.ImageFormats.FromFilename(path));
        }
    }

    public readonly List<FilePickerFileType> FilePickerFileTypes =
    [
        new("PNG Files") { Patterns = new List<string> { "*.png" } },
        new("JPEG Files") { Patterns = new List<string> { "*.jpg", "*.jpeg" } },
        new("BMP Files") { Patterns = new List<string> { "*.bmp" } },
        new("WebP Files") { Patterns = new List<string> { "*.webp" } },
        new("SVG Files") { Patterns = new List<string> { "*.svg" } },
        new("All Files") { Patterns = new List<string> { "*" } },
    ];

    public async void CopyToClipboard(SP.Plot plot)
    {
        if (TopLevel.GetTopLevel(_plotView)?.Clipboard is not { } clipboard) return;

        SP.PixelSize lastRenderSize = plot.RenderManager.LastRender.FigureRect.Size;
        var img = plot.GetImage((int)lastRenderSize.Width, (int)lastRenderSize.Height);
        var bytes = img.GetImageBytes(SP.ImageFormat.Bmp);

        using var stream = new System.IO.MemoryStream(bytes, BitmapHeaderSize, bytes.Length - BitmapHeaderSize);
        var bitmap = new Bitmap(stream);

        await clipboard.SetBitmapAsync(bitmap);
    }

    private const int BitmapHeaderSize = 14 + 40;

    public void Autoscale(SP.Plot plot)
    {
        plot.Axes.AutoScale();
        _plotView.Refresh();
    }

    public void ShowContextMenu(SP.Pixel pixel)
    {
        var plot = _plotView.Plot;
        if (plot is null || ContextMenuItems.Count == 0) return;

        var menu = GetContextMenu(plot);
        menu.PlacementRect = new(pixel.X, pixel.Y, 1, 1);
        menu.Open(_plotView);
    }

    public void Reset()
    {
        Clear();
        ContextMenuItems.AddRange(GetDefaultContextMenuItems());
    }

    public void Clear()
    {
        ContextMenuItems.Clear();
    }

    public void Add(string label, Action<SP.Plot> action)
    {
        ContextMenuItems.Add(new() { Label = label, OnInvoke = action });
    }

    public void AddSeparator()
    {
        ContextMenuItems.Add(new() { IsSeparator = true });
    }
}
