using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Ranil_Uchebka.Models;

namespace Ranil_Uchebka.Views;

public partial class GanttChartView : UserControl
{
    private const double LeftMargin = 140;
    private const double TopMargin = 28;
    private const double RowHeight = 34;
    private const double BarHeight = 22;
    private const double MinChartWidth = 820;

    public static readonly StyledProperty<IReadOnlyList<GanttRow>?> RowsProperty =
        AvaloniaProperty.Register<GanttChartView, IReadOnlyList<GanttRow>?>(nameof(Rows));

    private static readonly IBrush[] BarBrushes =
    [
        new SolidColorBrush(Color.Parse("#4867AC")),
        new SolidColorBrush(Color.Parse("#BB0C07")),
        new SolidColorBrush(Color.Parse("#F3CC8D")),
        new SolidColorBrush(Color.Parse("#4867AC")),
        new SolidColorBrush(Color.Parse("#BB0C07")),
        new SolidColorBrush(Color.Parse("#4867AC"))
    ];

    public IReadOnlyList<GanttRow>? Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public GanttChartView()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RowsProperty)
        {
            RenderChart();
        }
    }

    public void RenderChart()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RenderChart);
            return;
        }

        if (ChartCanvas is null)
        {
            return;
        }

        ChartCanvas.Children.Clear();
        var rows = Rows?.Where(r => r.DurationMinute > 0).ToList() ?? [];
        if (rows.Count == 0)
        {
            ChartCanvas.Height = 120;
            ChartCanvas.Width = MinChartWidth + LeftMargin;
            ChartCanvas.Children.Add(new TextBlock
            {
                Text = "Нет данных для диаграммы. Выполните расчёт заказа.",
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
                Width = 420
            });
            Canvas.SetLeft(ChartCanvas.Children[0], 12);
            Canvas.SetTop(ChartCanvas.Children[0], 40);
            return;
        }

        var maxEnd = rows.Max(r => r.EndMinute);
        if (maxEnd <= 0)
        {
            maxEnd = 1;
        }

        var equipmentGroups = rows
            .GroupBy(r => r.Equipment)
            .OrderBy(g => g.Key)
            .ToList();

        var chartHeight = TopMargin + equipmentGroups.Count * RowHeight + 36;
        var chartWidth = LeftMargin + MinChartWidth;
        ChartCanvas.Height = chartHeight;
        ChartCanvas.Width = chartWidth;

        DrawTimeAxis(maxEnd, chartWidth);

        var productColors = rows
            .Select(r => r.ProductName)
            .Distinct()
            .Select((name, index) => (name, BarBrushes[index % BarBrushes.Length]))
            .ToDictionary(x => x.name, x => x.Item2);

        for (var i = 0; i < equipmentGroups.Count; i++)
        {
            var group = equipmentGroups[i];
            var y = TopMargin + i * RowHeight;

            ChartCanvas.Children.Add(new TextBlock
            {
                Text = group.Key,
                Width = LeftMargin - 10,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 12,
                Foreground = Brushes.Black
            });
            Canvas.SetLeft(ChartCanvas.Children[^1], 4);
            Canvas.SetTop(ChartCanvas.Children[^1], y + 4);

            ChartCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Point(LeftMargin, y + RowHeight),
                EndPoint = new Point(chartWidth - 8, y + RowHeight),
                Stroke = new SolidColorBrush(Color.Parse("#F3CC8D")),
                StrokeThickness = 1
            });

            foreach (var row in group.OrderBy(r => r.StartMinute))
            {
                var x = LeftMargin + (row.StartMinute / (double)maxEnd) * (chartWidth - LeftMargin - 16);
                var width = Math.Max((row.DurationMinute / (double)maxEnd) * (chartWidth - LeftMargin - 16), 6);
                var brush = productColors.TryGetValue(row.ProductName, out var b) ? b : BarBrushes[0];

                var bar = new Border
                {
                    Width = width,
                    Height = BarHeight,
                    Background = brush,
                    CornerRadius = new CornerRadius(3),
                    Child = new TextBlock
                    {
                        Text = row.OperationName,
                        Foreground = Brushes.White,
                        FontSize = 10,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        Margin = new Thickness(4, 0)
                    }
                };
                ToolTip.SetTip(bar, $"{row.ProductName}\n{row.OperationName}\n{row.StartMinute}–{row.EndMinute} мин.");

                ChartCanvas.Children.Add(bar);
                Canvas.SetLeft(bar, x);
                Canvas.SetTop(bar, y + (RowHeight - BarHeight) / 2);
            }
        }

        DrawLegend(productColors, chartWidth);
    }

    private void DrawTimeAxis(int maxEnd, double chartWidth)
    {
        var steps = Math.Min(8, Math.Max(4, maxEnd / 30));
        var step = Math.Max(1, (int)Math.Ceiling(maxEnd / (double)steps));

        ChartCanvas.Children.Add(new TextBlock
        {
            Text = "Время (мин.)",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.Black
        });
        Canvas.SetLeft(ChartCanvas.Children[^1], LeftMargin);
        Canvas.SetTop(ChartCanvas.Children[^1], 4);

        for (var minute = 0; minute <= maxEnd; minute += step)
        {
            var x = LeftMargin + (minute / (double)maxEnd) * (chartWidth - LeftMargin - 16);
            ChartCanvas.Children.Add(new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Point(x, TopMargin - 4),
                EndPoint = new Point(x, ChartCanvas.Height - 8),
                Stroke = new SolidColorBrush(Color.Parse("#F1F1F1")),
                StrokeThickness = 1
            });

            ChartCanvas.Children.Add(new TextBlock
            {
                Text = minute.ToString(),
                FontSize = 10,
                Foreground = Brushes.Black
            });
            Canvas.SetLeft(ChartCanvas.Children[^1], x - 8);
            Canvas.SetTop(ChartCanvas.Children[^1], TopMargin - 18);
        }
    }

    private void DrawLegend(IReadOnlyDictionary<string, IBrush> productColors, double chartWidth)
    {
        var legendY = ChartCanvas.Height - 14;
        var legendX = LeftMargin;
        foreach (var (product, brush) in productColors)
        {
            ChartCanvas.Children.Add(new Border
            {
                Width = 12,
                Height = 12,
                Background = brush,
                CornerRadius = new CornerRadius(2)
            });
            Canvas.SetLeft(ChartCanvas.Children[^1], legendX);
            Canvas.SetTop(ChartCanvas.Children[^1], legendY);

            ChartCanvas.Children.Add(new TextBlock
            {
                Text = product,
                FontSize = 10,
                Foreground = Brushes.Black
            });
            Canvas.SetLeft(ChartCanvas.Children[^1], legendX + 16);
            Canvas.SetTop(ChartCanvas.Children[^1], legendY - 2);

            legendX += 16 + Math.Min(product.Length * 6.5, 120) + 20;
            if (legendX > chartWidth - 80)
            {
                break;
            }
        }
    }
}
