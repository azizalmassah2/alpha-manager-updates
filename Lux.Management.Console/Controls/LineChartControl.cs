using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Lux.Platform.Abstractions.Models.Monitoring;

namespace Lux.Management.Console.Controls;

public class LineChartControl : Control
{
    private Canvas? _canvas;

    public static readonly DependencyProperty MetricsProperty =
        DependencyProperty.Register(
            nameof(Metrics),
            typeof(IEnumerable<DeviceMetric>),
            typeof(LineChartControl),
            new PropertyMetadata(null, OnMetricsChanged));

    public IEnumerable<DeviceMetric> Metrics
    {
        get => (IEnumerable<DeviceMetric>)GetValue(MetricsProperty);
        set => SetValue(MetricsProperty, value);
    }

    public static readonly DependencyProperty LineColorProperty =
        DependencyProperty.Register(
            nameof(LineColor),
            typeof(Brush),
            typeof(LineChartControl),
            new PropertyMetadata(Brushes.CornflowerBlue));

    public Brush LineColor
    {
        get => (Brush)GetValue(LineColorProperty);
        set => SetValue(LineColorProperty, value);
    }

    static LineChartControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(LineChartControl), new FrameworkPropertyMetadata(typeof(LineChartControl)));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _canvas = GetTemplateChild("PART_Canvas") as Canvas;
        DrawChart();
    }

    private static void OnMetricsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LineChartControl control)
        {
            control.DrawChart();
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        DrawChart();
    }

    public enum MetricProperty { CpuUsage, MemoryUsage }

    public static readonly DependencyProperty PropertyToChartProperty =
        DependencyProperty.Register(
            nameof(PropertyToChart),
            typeof(MetricProperty),
            typeof(LineChartControl),
            new PropertyMetadata(MetricProperty.CpuUsage, OnMetricsChanged));

    public MetricProperty PropertyToChart
    {
        get => (MetricProperty)GetValue(PropertyToChartProperty);
        set => SetValue(PropertyToChartProperty, value);
    }

    private void DrawChart()
    {
        if (_canvas == null || Metrics == null) return;

        _canvas.Children.Clear();

        var metricsList = Metrics.OrderBy(m => m.Timestamp).ToList();
        if (metricsList.Count < 2) return;

        double width = ActualWidth;
        double height = ActualHeight;

        if (width <= 0 || height <= 0) return;

        Func<DeviceMetric, double> selector = PropertyToChart == MetricProperty.CpuUsage 
            ? m => m.CpuUsage 
            : m => m.MemoryUsage;

        double maxVal = metricsList.Max(selector);
        double minVal = metricsList.Min(selector);
        if (maxVal == minVal)
        {
            maxVal += 1;
            minVal -= 1;
        }

        double timeRange = (metricsList.Last().Timestamp - metricsList.First().Timestamp).TotalSeconds;
        if (timeRange <= 0) timeRange = 1;

        var points = new PointCollection();
        foreach (var m in metricsList)
        {
            double val = selector(m);
            double x = ((m.Timestamp - metricsList.First().Timestamp).TotalSeconds / timeRange) * width;
            double y = height - ((val - minVal) / (maxVal - minVal) * height);
            points.Add(new Point(x, y));
        }

        var polyline = new Polyline
        {
            Stroke = LineColor,
            StrokeThickness = 2,
            Points = points
        };

        _canvas.Children.Add(polyline);
    }
}
