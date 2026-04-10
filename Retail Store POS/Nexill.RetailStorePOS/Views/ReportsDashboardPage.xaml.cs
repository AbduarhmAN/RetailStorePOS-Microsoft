using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.ObjectModel;
using Windows.Foundation;
using Windows.UI;

namespace RetailStorePOS.WinUiLogin.Views;

// ─── Data models ──────────────────────────────────────────────────────────────

public sealed class DashboardProductItem
{
    public string Name    { get; set; } = string.Empty;
    public string Revenue { get; set; } = string.Empty;
    public double Pct     { get; set; }
}

public sealed class DashboardStockItem
{
    public string Name        { get; set; } = string.Empty;
    public string StockStatus { get; set; } = string.Empty;
    public SolidColorBrush StatusBrush { get; set; } = new(Color.FromArgb(255, 143, 150, 163));
    public string BadgeText   { get; set; } = string.Empty;
    public Visibility BadgeVisible { get; set; } = Visibility.Visible;
}

// ─── Page ─────────────────────────────────────────────────────────────────────

public sealed partial class ReportsDashboardPage : Page
{
    public ReportsDashboardPage()
    {
        this.InitializeComponent();
        this.Loaded += (_, _) => PopulateDashboard();
    }

    private void PopulateDashboard()
    {
        // ── HEADER ─────────────────────────────────────────
        var hour = DateTime.Now.Hour;
        GreetingText.Text  = hour < 12 ? "Good morning, Store Manager"
                           : hour < 17 ? "Good afternoon, Store Manager"
                                       : "Good evening, Store Manager";
        TodayDateText.Text = DateTime.Now.ToString("MMMM dd, yyyy");

        // ── KPI CARDS ──────────────────────────────────────
        SalesTodayText.Text   = "$15,800";
        SalesDeltaText.Text   = "+ 18% vs yesterday";
        ProfitTodayText.Text  = "$3,200";
        ProfitDeltaText.Text  = "+ 14% vs yesterday";
        InvoiceCountText.Text = "205";
        InvoiceDeltaText.Text = "+ 10% vs yesterday";
        AvgInvoiceText.Text   = "$77";
        MedianText.Text       = "$58";

        // ── SALES VS TARGET ────────────────────────────────
        CurrentSalesText.Text = "$15,800";
        SalesGapText.Text     = "-47%";
        TargetText.Text       = "$30,000";
        TargetSubText.Text    = "-47% vs target  ·  TARGET: $30,000";

        // Wire bar widths when the container is measured
        SalesBarContainer.SizeChanged += (_, _) => UpdateSalesBar(todayPct: 0.53, yesterdayPct: 0.45);

        // ── DISCOUNTS ──────────────────────────────────────
        DiscountsTotalText.Text = "$540";
        DiscountsDeltaText.Text = "+3.4%";
        DiscountsCountText.Text = "7 Discounts Applied";

        // ── TOP PRODUCTS ───────────────────────────────────
        TopProductsList.ItemsSource = new ObservableCollection<DashboardProductItem>
        {
            new() { Name = "Cola",              Revenue = "$72,500", Pct = 100 },
            new() { Name = "Whole Milk",         Revenue = "$62,300", Pct = 86  },
            new() { Name = "Potato Chips",       Revenue = "$48,720", Pct = 67  },
            new() { Name = "Water 330ml",        Revenue = "$21,375", Pct = 29  },
            new() { Name = "Chocolate Biscuits", Revenue = "$41,555", Pct = 57  },
        };

        // ── LOW STOCK LIST ──────────────────────────────────
        var amber = new SolidColorBrush(Color.FromArgb(255, 255, 143, 0));
        var red   = new SolidColorBrush(Color.FromArgb(255, 229,  57, 53));

        LowStockList.ItemsSource = new ObservableCollection<DashboardStockItem>
        {
            new() { Name = "Cola",        StockStatus = "4 left",       StatusBrush = amber, BadgeText = "+4", BadgeVisible = Visibility.Visible   },
            new() { Name = "Cooking Oil", StockStatus = "6 left",       StatusBrush = amber, BadgeText = "+6", BadgeVisible = Visibility.Visible   },
            new() { Name = "Green Tea",   StockStatus = "Out of Stock",  StatusBrush = red,   BadgeText = "",   BadgeVisible = Visibility.Collapsed },
        };

        // ── DONUT CHART ────────────────────────────────────
        DrawDonutChart(
            canvas: StockHealthCanvas,
            centerLabel: DonutCenterText,
            segments: new[]
            {
                (0.51, Color.FromArgb(255, 25, 118, 210)),   // Blue  – Healthy
                (0.29, Color.FromArgb(255, 249, 168,  37)),  // Yellow – Low Stock
                (0.20, Color.FromArgb(255, 229,  57,  53)),  // Red   – Out of Stock
            },
            canvasSize: 140,
            radius: 48,
            thickness: 16
        );
    }

    // ── Donut drawing engine ──────────────────────────────────────────────────
    private static void DrawDonutChart(
        Canvas canvas,
        TextBlock centerLabel,
        (double Pct, Color Color)[] segments,
        double canvasSize,
        double radius,
        double thickness)
    {
        canvas.Children.Clear();
        double cx = canvasSize / 2;
        double cy = canvasSize / 2;

        // Gray background track
        var track = new Ellipse
        {
            Width  = radius * 2,
            Height = radius * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(255, 232, 234, 240)),
            StrokeThickness = thickness,
            Fill = null
        };
        Canvas.SetLeft(track, cx - radius);
        Canvas.SetTop(track,  cy - radius);
        canvas.Children.Add(track);

        // Draw each arc segment
        double startAngle = -90.0; // start at top
        foreach (var (pct, color) in segments)
        {
            if (pct <= 0) continue;

            double sweepAngle = pct * 360.0;

            // Clamp slightly to avoid path collapsing at exactly 360
            if (sweepAngle >= 360) sweepAngle = 359.99;

            var path = CreateArcPath(cx, cy, radius, startAngle, sweepAngle, color, thickness);
            canvas.Children.Add(path);

            startAngle += sweepAngle;
        }

        // Set center label to the largest segment
        var dominant = segments[0];
        centerLabel.Text = $"{dominant.Pct * 100:0}%";
    }

    private static Microsoft.UI.Xaml.Shapes.Path CreateArcPath(
        double cx, double cy, double radius,
        double startDeg, double sweepDeg,
        Color color, double thickness)
    {
        double startRad = startDeg * Math.PI / 180.0;
        double endRad   = (startDeg + sweepDeg) * Math.PI / 180.0;

        var startPt = new Point(cx + radius * Math.Cos(startRad), cy + radius * Math.Sin(startRad));
        var endPt   = new Point(cx + radius * Math.Cos(endRad),   cy + radius * Math.Sin(endRad));

        var figure = new PathFigure
        {
            StartPoint = startPt,
            IsClosed   = false
        };

        figure.Segments.Add(new ArcSegment
        {
            Point          = endPt,
            Size           = new Size(radius, radius),
            IsLargeArc     = sweepDeg > 180,
            SweepDirection = SweepDirection.Clockwise,
            RotationAngle  = 0
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        return new Microsoft.UI.Xaml.Shapes.Path
        {
            Data            = geometry,
            Stroke          = new SolidColorBrush(color),
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap   = PenLineCap.Round,
            Fill            = null
        };
    }

    private void UpdateSalesBar(double todayPct, double yesterdayPct)
    {
        double containerWidth = SalesBarContainer.ActualWidth;
        if (containerWidth <= 0) return;

        YesterdayBar.Width = containerWidth * yesterdayPct;
        TodayBar.Width = containerWidth * todayPct;
    }
}
