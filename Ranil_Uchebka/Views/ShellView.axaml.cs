using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Controls.Primitives;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Platform.Storage;
using Ranil_Uchebka.Models;
using Ranil_Uchebka.ViewModels;

namespace Ranil_Uchebka.Views;

public partial class ShellView : UserControl
{
    private const double WorkshopCanvasWidth = 1000;
    private const double WorkshopCanvasHeight = 520;
    private const double MarkerSize = 36;

    private MainViewModel? _subscribedVm;
    private Border? _draggingMarkerControl;
    private WorkshopMarkerModel? _draggingMarker;
    private Point _dragOffset;
    private readonly Dictionary<string, Bitmap> _iconCache = [];

    public ShellView()
    {
        InitializeComponent();
        DataContextChanged += OnShellDataContextChanged;
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private Window? OwnerWindow => VisualRoot as Window;

    private void OnShellDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedVm is not null)
        {
            _subscribedVm.PropertyChanged -= OnVmPropertyChanged;
            _subscribedVm.DraftWorkshopMarkers.CollectionChanged -= OnDraftWorkshopMarkersChanged;
        }

        _subscribedVm = Vm;
        if (_subscribedVm is not null)
        {
            _subscribedVm.PropertyChanged += OnVmPropertyChanged;
            _subscribedVm.DraftWorkshopMarkers.CollectionChanged += OnDraftWorkshopMarkersChanged;
        }

        RenderWorkshopCanvas();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedWorkshop))
        {
            RenderWorkshopCanvas();
        }
    }

    private void OnDraftWorkshopMarkersChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RenderWorkshopCanvas();

    private async void DeleteMaterialClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || OwnerWindow is null ||
            !await ShowConfirmAsync(OwnerWindow, "Удалить материал?", "Удаление возможно только при нулевом количестве. Продолжить?"))
        {
            return;
        }

        var (ok, message) = await Vm.DeleteSelectedMaterialAsync();
        if (!ok)
        {
            await ShowInfoAsync(OwnerWindow, "Удаление материала", message);
        }
    }

    private async void DeleteComponentClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || OwnerWindow is null ||
            !await ShowConfirmAsync(OwnerWindow, "Удалить комплектующее?", "Удаление возможно только при нулевом количестве. Продолжить?"))
        {
            return;
        }

        var (ok, message) = await Vm.DeleteSelectedComponentAsync();
        if (!ok)
        {
            await ShowInfoAsync(OwnerWindow, "Удаление комплектующего", message);
        }
    }

    private async void AddOrderFilesClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null)
        {
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null)
        {
            return;
        }

        var picked = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Чертежи и схемы заказа",
            AllowMultiple = true,
            FileTypeFilter = [FilePickerFileTypes.All]
        });

        if (picked.Count == 0)
        {
            return;
        }

        var files = new List<(string fileName, byte[] data)>();
        foreach (var file in picked)
        {
            await using var stream = await file.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            files.Add((file.Name, ms.ToArray()));
        }

        Vm.AddPendingOrderAttachments(files);
    }

    private async void SaveOrderAttachmentClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || sender is not Button { Tag: OrderAttachmentRow row })
        {
            return;
        }

        var attachment = await Vm.GetOrderAttachmentForExportAsync(row);
        if (attachment is null)
        {
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null)
        {
            return;
        }

        var saveFile = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Сохранить вложение заказа",
            SuggestedFileName = attachment.Value.fileName
        });
        if (saveFile is null)
        {
            return;
        }

        await using var writeStream = await saveFile.OpenWriteAsync();
        await writeStream.WriteAsync(attachment.Value.data);
    }

    private async void ExportSpecificationClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null)
        {
            return;
        }

        await ExportTextAsync("Печать спецификации", $"{Vm.SpecProductName}_spec.txt", Vm.BuildSpecificationPrintText());
    }

    private async void ExportPlanningClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null)
        {
            return;
        }

        await ExportTextAsync("Печать расчёта заказа", $"{Vm.PlanningOrder?.OrderCode ?? "order"}_planning.txt", Vm.BuildPlanningPrintText());
    }

    private async void ExportStockReportClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null)
        {
            return;
        }

        await ExportTextAsync("Печать отчёта по остаткам", "stock_report.txt", Vm.BuildStockReportPrintText());
    }

    private async void AddSpecFilesClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null)
        {
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null)
        {
            return;
        }

        var picked = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Чертежи и схемы изделия",
            AllowMultiple = true,
            FileTypeFilter = [FilePickerFileTypes.All]
        });

        if (picked.Count == 0)
        {
            return;
        }

        var files = new List<(string fileName, byte[] data)>();
        foreach (var file in picked)
        {
            await using var stream = await file.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            files.Add((file.Name, ms.ToArray()));
        }

        Vm.AddPendingSpecAttachments(files);
    }

    private async void SaveSpecAttachmentClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || sender is not Button { Tag: ProductAttachmentRow row })
        {
            return;
        }

        var attachment = await Vm.GetProductAttachmentForExportAsync(row);
        if (attachment is null)
        {
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null)
        {
            return;
        }

        var saveFile = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Сохранить чертёж изделия",
            SuggestedFileName = attachment.Value.fileName
        });
        if (saveFile is null)
        {
            return;
        }

        await using var writeStream = await saveFile.OpenWriteAsync();
        await writeStream.WriteAsync(attachment.Value.data);
    }

    private async Task ExportTextAsync(string title, string suggestedFileName, string text)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null)
        {
            return;
        }

        var saveFile = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName
        });
        if (saveFile is null)
        {
            return;
        }

        await using var stream = await saveFile.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(text);
    }

    private async void WorkshopIconPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { Tag: string iconType })
        {
            return;
        }

        var data = new DataObject();
        data.Set(DataFormats.Text, iconType);
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
    }

    private void WorkshopCanvasDragOver(object? sender, DragEventArgs e)
    {
        var iconType = e.Data.Get(DataFormats.Text) as string;
        e.DragEffects = string.IsNullOrWhiteSpace(iconType) ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
    }

    private void WorkshopCanvasDrop(object? sender, DragEventArgs e)
    {
        if (Vm is null || Vm.SelectedWorkshop is null)
        {
            return;
        }

        var iconType = e.Data.Get(DataFormats.Text) as string;
        if (string.IsNullOrWhiteSpace(iconType))
        {
            return;
        }

        var p = e.GetPosition(WorkshopCanvas);
        var left = Math.Clamp(p.X - MarkerSize / 2, 0, WorkshopCanvasWidth - MarkerSize);
        var top = Math.Clamp(p.Y - MarkerSize / 2, 0, WorkshopCanvasHeight - MarkerSize);
        Vm.AddWorkshopMarkerAt(iconType, ToNormX(left), ToNormY(top));
        RenderWorkshopCanvas();
    }

    private void WorkshopZoomChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (WorkshopCanvas is null || WorkshopZoomText is null)
        {
            return;
        }

        var scale = e.NewValue;
        WorkshopCanvas.RenderTransform = new ScaleTransform(scale, scale);
        WorkshopCanvas.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
        WorkshopZoomText.Text = $"{scale * 100:0}%";
    }

    private void MarkerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border markerBorder || markerBorder.Tag is not WorkshopMarkerModel marker || Vm is null)
        {
            return;
        }

        Vm.SelectWorkshopMarker(marker);
        _draggingMarker = marker;
        _draggingMarkerControl = markerBorder;
        _dragOffset = e.GetPosition(markerBorder);
        e.Pointer.Capture(_draggingMarkerControl);
        e.Handled = true;
    }

    private void MarkerPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggingMarkerControl is null || _draggingMarker is null || Vm is null)
        {
            return;
        }

        if (!ReferenceEquals(e.Pointer.Captured, _draggingMarkerControl))
        {
            return;
        }

        var p = e.GetPosition(WorkshopCanvas);
        var left = Math.Clamp(p.X - _dragOffset.X, 0, WorkshopCanvasWidth - MarkerSize);
        var top = Math.Clamp(p.Y - _dragOffset.Y, 0, WorkshopCanvasHeight - MarkerSize);

        Canvas.SetLeft(_draggingMarkerControl, left);
        Canvas.SetTop(_draggingMarkerControl, top);
        Vm.MoveWorkshopMarker(_draggingMarker, ToNormX(left), ToNormY(top));
        e.Handled = true;
    }

    private void MarkerPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggingMarkerControl is not null && ReferenceEquals(e.Pointer.Captured, _draggingMarkerControl))
        {
            e.Pointer.Capture(null);
        }

        _draggingMarkerControl = null;
        _draggingMarker = null;
        RenderWorkshopCanvas();
    }

    private void RenderWorkshopCanvas()
    {
        if (WorkshopCanvas is null)
        {
            return;
        }

        WorkshopCanvas.Children.Clear();
        var vm = Vm;
        if (vm?.SelectedWorkshop is null)
        {
            WorkshopCanvas.Children.Add(new TextBlock
            {
                Text = "Выберите цех для отображения плана.",
                Margin = new Thickness(12),
                Foreground = Brushes.Black
            });
            return;
        }

        var planImage = new Image
        {
            Width = WorkshopCanvasWidth,
            Height = WorkshopCanvasHeight,
            Stretch = Stretch.Fill,
            Source = LoadPlanBitmap(vm.SelectedWorkshop.PlanFileName)
        };
        Canvas.SetLeft(planImage, 0);
        Canvas.SetTop(planImage, 0);
        WorkshopCanvas.Children.Add(planImage);

        foreach (var marker in vm.DraftWorkshopMarkers)
        {
            var markerBorder = new Border
            {
                Width = MarkerSize,
                Height = MarkerSize,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.Parse("#FEF9DE")),
                BorderThickness = new Thickness(2),
                BorderBrush = ReferenceEquals(marker, vm.SelectedDraftWorkshopMarker)
                    ? new SolidColorBrush(Color.Parse("#BB0C07"))
                    : new SolidColorBrush(Color.Parse("#4867AC")),
                Tag = marker,
                Child = new Image
                {
                    Stretch = Stretch.Uniform,
                    Source = LoadIconBitmap(marker.IconType)
                }
            };
            markerBorder.PointerPressed += MarkerPointerPressed;
            markerBorder.PointerMoved += MarkerPointerMoved;
            markerBorder.PointerReleased += MarkerPointerReleased;

            Canvas.SetLeft(markerBorder, ToCanvasX(marker.X));
            Canvas.SetTop(markerBorder, ToCanvasY(marker.Y));
            WorkshopCanvas.Children.Add(markerBorder);
        }
    }

    private static double ToNormX(double left) => Math.Clamp(left / (WorkshopCanvasWidth - MarkerSize), 0, 1);
    private static double ToNormY(double top) => Math.Clamp(top / (WorkshopCanvasHeight - MarkerSize), 0, 1);
    private static double ToCanvasX(double x) => Math.Clamp(x, 0, 1) * (WorkshopCanvasWidth - MarkerSize);
    private static double ToCanvasY(double y) => Math.Clamp(y, 0, 1) * (WorkshopCanvasHeight - MarkerSize);

    private Bitmap? LoadPlanBitmap(string fileName)
    {
        var escaped = Uri.EscapeDataString(fileName);
        var uri = new Uri($"avares://Ranil_Uchebka/Assets/Workshops/{escaped}");
        if (!AssetLoader.Exists(uri))
        {
            return null;
        }

        using var stream = AssetLoader.Open(uri);
        return new Bitmap(stream);
    }

    private Bitmap? LoadIconBitmap(string iconType)
    {
        if (_iconCache.TryGetValue(iconType, out var cached))
        {
            return cached;
        }

        var fileName = iconType switch
        {
            "Equipment" => "Equipment.png",
            "FireExtinguisher" => "FireExtinguisher.png",
            "FirstAid" => "FirstAid.png",
            "Exit" => "Exit.jpg",
            _ => "Equipment.png"
        };
        var escaped = Uri.EscapeDataString(fileName);
        var uri = new Uri($"avares://Ranil_Uchebka/Assets/Icons/{escaped}");
        if (!AssetLoader.Exists(uri))
        {
            return null;
        }

        using var stream = AssetLoader.Open(uri);
        var bitmap = new Bitmap(stream);
        _iconCache[iconType] = bitmap;
        return bitmap;
    }

    private async void DeleteWorkerClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null || OwnerWindow is null ||
            !await ShowConfirmAsync(OwnerWindow, "Удалить работника?", "Запись работника и связи с операциями будут удалены. Продолжить?"))
        {
            return;
        }

        var (ok, message) = await Vm.DeleteWorkerAsync();
        if (!ok)
        {
            await ShowInfoAsync(OwnerWindow, "Удаление работника", message);
        }
    }

    private static async Task<bool> ShowConfirmAsync(Window owner, string title, string text)
    {
        var tcs = new TaskCompletionSource<bool>();
        var dialog = new Window
        {
            Width = 420,
            Height = 190,
            CanResize = false,
            Title = title,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var yesButton = new Button { Content = "Да", Width = 90, Classes = { "primary" } };
        var noButton = new Button { Content = "Нет", Width = 90, Classes = { "danger" } };
        yesButton.Click += (_, _) =>
        {
            tcs.TrySetResult(true);
            dialog.Close();
        };
        noButton.Click += (_, _) =>
        {
            tcs.TrySetResult(false);
            dialog.Close();
        };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { yesButton, noButton }
                }
            }
        };

        await dialog.ShowDialog(owner);
        return await tcs.Task;
    }

    private static async Task ShowInfoAsync(Window owner, string title, string text)
    {
        var dialog = new Window
        {
            Width = 420,
            Height = 180,
            CanResize = false,
            Title = title,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var okButton = new Button
        {
            Content = "ОК",
            Width = 90,
            HorizontalAlignment = HorizontalAlignment.Right,
            Classes = { "primary" }
        };
        okButton.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
                okButton
            }
        };

        await dialog.ShowDialog(owner);
    }
}
