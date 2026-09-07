using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Poe2MarketScanner.Core.Configuration;

namespace Poe2MarketScanner.App;

public partial class OverlayWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly Dictionary<RegionEntry, RegionVisuals> _regionVisuals = new();
    private readonly Dictionary<AnchorEntry, FrameworkElement> _anchorVisuals = new();
    private readonly DispatcherTimer _hoverTimer;
    private DragContext? _dragContext;
    private RegionEntry? _hoveredRegion;
    private Point _lastPointerPosition;
    private DateTime _stationarySince;

    public OverlayWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        RenderProfile();
        _viewModel.LayoutChanged += HandleLayoutChanged;
        _hoverTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Input, HandleHoverTimerTick, Dispatcher);
        _hoverTimer.Start();
        SetEditing(true);
    }

    public void SetEditing(bool editing)
    {
        OverlayCanvas.IsHitTestVisible = editing;
        OverlayStateText.Text = editing
            ? "编辑状态：拖动框体可移动，拖动右下角手柄可缩放，拖动锚点可调整点击位置。"
            : "观察状态：标注层已锁定，不会阻挡游戏操作。";
    }

    protected override void OnClosed(EventArgs e)
    {
        _hoverTimer.Stop();
        _viewModel.LayoutChanged -= HandleLayoutChanged;
        base.OnClosed(e);
    }

    private void HandleLayoutChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(UpdateAllVisuals);
    }

    private void RenderProfile()
    {
        OverlayCanvas.Children.Clear();
        _regionVisuals.Clear();
        _anchorVisuals.Clear();

        foreach (var regionEntry in _viewModel.RegionEntries)
        {
            var visuals = CreateRegionVisuals(regionEntry);
            _regionVisuals[regionEntry] = visuals;
            OverlayCanvas.Children.Add(visuals.Container);
            OverlayCanvas.Children.Add(visuals.ResizeHandle);
            OverlayCanvas.Children.Add(visuals.Label);
            UpdateRegionVisual(regionEntry);
        }

        foreach (var anchorEntry in _viewModel.AnchorEntries)
        {
            var anchor = CreateAnchorElement(anchorEntry);
            _anchorVisuals[anchorEntry] = anchor;
            OverlayCanvas.Children.Add(anchor);
            UpdateAnchorVisual(anchorEntry);
        }
    }

    private void UpdateAllVisuals()
    {
        foreach (var regionEntry in _viewModel.RegionEntries)
        {
            UpdateRegionVisual(regionEntry);
        }

        foreach (var anchorEntry in _viewModel.AnchorEntries)
        {
            UpdateAnchorVisual(anchorEntry);
        }
    }

    private RegionVisuals CreateRegionVisuals(RegionEntry entry)
    {
        var brush = CreateBrush(entry.Region.Color);
        var container = new Grid
        {
            Cursor = Cursors.SizeAll
        };

        container.Children.Add(new Border
        {
            BorderBrush = brush,
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(brush.Color) { Opacity = 0.12 }
        });

        var label = new Border
        {
            Background = brush,
            Padding = new Thickness(8, 4, 8, 4),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = entry.Region.Name,
                Foreground = Brushes.Black,
                FontWeight = FontWeights.Bold
            }
        };

        var resizeHandle = new Ellipse
        {
            Width = 18,
            Height = 18,
            Fill = brush,
            Stroke = Brushes.White,
            StrokeThickness = 1.5,
            Cursor = Cursors.SizeNWSE
        };

        container.MouseLeftButtonDown += (_, e) => BeginMoveRegion(entry, container, e);
        container.MouseMove += (_, e) => ContinueDrag(e);
        container.MouseLeftButtonUp += (_, e) => EndDrag(container);

        resizeHandle.MouseLeftButtonDown += (_, e) => BeginResizeRegion(entry, resizeHandle, e);
        resizeHandle.MouseMove += (_, e) => ContinueDrag(e);
        resizeHandle.MouseLeftButtonUp += (_, e) => EndDrag(resizeHandle);

        return new RegionVisuals(container, resizeHandle, label);
    }

    private void HandleHoverTimerTick(object? sender, EventArgs e)
    {
        var pointer = Mouse.GetPosition(this);
        var region = FindRegionAt(pointer);
        if (region is null)
        {
            ResetRegionHover(null, pointer);
            return;
        }

        var moved = Math.Abs(pointer.X - _lastPointerPosition.X) > 2 ||
                    Math.Abs(pointer.Y - _lastPointerPosition.Y) > 2;
        if (!ReferenceEquals(region, _hoveredRegion) || moved)
        {
            ResetRegionHover(region, pointer);
            return;
        }

        if (DateTime.UtcNow - _stationarySince >= TimeSpan.FromMilliseconds(500))
        {
            ShowRegionLabel(region);
        }
    }

    private RegionEntry? FindRegionAt(Point pointer)
    {
        foreach (var entry in _viewModel.RegionEntries)
        {
            var left = entry.Region.X - Left;
            var top = entry.Region.Y - Top;
            if (pointer.X >= left && pointer.X <= left + entry.Region.Width &&
                pointer.Y >= top && pointer.Y <= top + entry.Region.Height)
            {
                return entry;
            }
        }

        return null;
    }

    private void ResetRegionHover(RegionEntry? region, Point pointer)
    {
        foreach (var visuals in _regionVisuals.Values)
        {
            visuals.Label.Visibility = Visibility.Collapsed;
        }

        _hoveredRegion = region;
        _lastPointerPosition = pointer;
        _stationarySince = DateTime.UtcNow;
    }

    private void ShowRegionLabel(RegionEntry entry)
    {
        var label = _regionVisuals[entry].Label;
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var left = entry.Region.X - Left;
        var regionTop = entry.Region.Y - Top;
        var top = regionTop - label.DesiredSize.Height - 6;
        if (top < 0)
        {
            top = regionTop + entry.Region.Height + 6;
        }

        Canvas.SetLeft(label, Math.Max(0, Math.Min(left, ActualWidth - label.DesiredSize.Width)));
        Canvas.SetTop(label, top);
        label.Visibility = Visibility.Visible;
    }

    private FrameworkElement CreateAnchorElement(AnchorEntry entry)
    {
        var panel = new Grid
        {
            Width = 140,
            Height = 30,
            Cursor = Cursors.Hand
        };

        panel.Children.Add(new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = Brushes.Cyan,
            Stroke = Brushes.White,
            StrokeThickness = 1.2,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        });

        panel.Children.Add(new TextBlock
        {
            Margin = new Thickness(20, 0, 0, 0),
            Text = entry.Anchor.Name,
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        panel.MouseLeftButtonDown += (_, e) => BeginMoveAnchor(entry, panel, e);
        panel.MouseMove += (_, e) => ContinueDrag(e);
        panel.MouseLeftButtonUp += (_, e) => EndDrag(panel);

        return panel;
    }

    private void BeginMoveRegion(RegionEntry entry, FrameworkElement element, MouseButtonEventArgs e)
    {
        _dragContext = DragContext.ForMoveRegion(this, element, entry);
        element.CaptureMouse();
        e.Handled = true;
    }

    private void BeginResizeRegion(RegionEntry entry, FrameworkElement element, MouseButtonEventArgs e)
    {
        _dragContext = DragContext.ForResizeRegion(this, element, entry);
        element.CaptureMouse();
        e.Handled = true;
    }

    private void BeginMoveAnchor(AnchorEntry entry, FrameworkElement element, MouseButtonEventArgs e)
    {
        _dragContext = DragContext.ForMoveAnchor(this, element, entry);
        element.CaptureMouse();
        e.Handled = true;
    }

    private void ContinueDrag(MouseEventArgs e)
    {
        if (_dragContext is null)
        {
            return;
        }

        var currentPoint = e.GetPosition(this);
        var delta = currentPoint - _dragContext.StartPoint;

        switch (_dragContext.Mode)
        {
            case DragMode.MoveRegion:
                _dragContext.RegionEntry!.Region.X = _dragContext.StartX + delta.X;
                _dragContext.RegionEntry.Region.Y = _dragContext.StartY + delta.Y;
                UpdateRegionVisual(_dragContext.RegionEntry);
                break;

            case DragMode.ResizeRegion:
                _dragContext.RegionEntry!.Region.Width = Math.Max(40, _dragContext.StartWidth + delta.X);
                _dragContext.RegionEntry.Region.Height = Math.Max(1, _dragContext.StartHeight + delta.Y);
                UpdateRegionVisual(_dragContext.RegionEntry);
                break;

            case DragMode.MoveAnchor:
                _dragContext.AnchorEntry!.Anchor.X = _dragContext.StartX + delta.X;
                _dragContext.AnchorEntry.Anchor.Y = _dragContext.StartY + delta.Y;
                UpdateAnchorVisual(_dragContext.AnchorEntry);
                break;
        }
    }

    private void EndDrag(FrameworkElement element)
    {
        element.ReleaseMouseCapture();
        _dragContext = null;
    }

    private void UpdateRegionVisual(RegionEntry entry)
    {
        if (!_regionVisuals.TryGetValue(entry, out var visuals))
        {
            return;
        }

        visuals.Container.Width = entry.Region.Width;
        visuals.Container.Height = entry.Region.Height;
        Canvas.SetLeft(visuals.Container, entry.Region.X - Left);
        Canvas.SetTop(visuals.Container, entry.Region.Y - Top);

        Canvas.SetLeft(visuals.ResizeHandle, entry.Region.X - Left + entry.Region.Width - visuals.ResizeHandle.Width / 2);
        Canvas.SetTop(visuals.ResizeHandle, entry.Region.Y - Top + entry.Region.Height - visuals.ResizeHandle.Height / 2);
    }

    private void UpdateAnchorVisual(AnchorEntry entry)
    {
        if (!_anchorVisuals.TryGetValue(entry, out var element))
        {
            return;
        }

        Canvas.SetLeft(element, AnchorVisualGeometry.GetVisualLeft(entry.Anchor.X) - Left);
        Canvas.SetTop(element, AnchorVisualGeometry.GetVisualTop(entry.Anchor.Y) - Top);
    }

    private static SolidColorBrush CreateBrush(string colorHex)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
    }

    private sealed record RegionVisuals(FrameworkElement Container, FrameworkElement ResizeHandle, FrameworkElement Label);

    private sealed class DragContext
    {
        public DragMode Mode { get; init; }
        public Point StartPoint { get; init; }
        public double StartX { get; init; }
        public double StartY { get; init; }
        public double StartWidth { get; init; }
        public double StartHeight { get; init; }
        public RegionEntry? RegionEntry { get; init; }
        public AnchorEntry? AnchorEntry { get; init; }

        public static DragContext ForMoveRegion(Window owner, FrameworkElement _, RegionEntry entry)
        {
            return new DragContext
            {
                Mode = DragMode.MoveRegion,
                StartPoint = Mouse.GetPosition(owner),
                StartX = entry.Region.X,
                StartY = entry.Region.Y,
                StartWidth = entry.Region.Width,
                StartHeight = entry.Region.Height,
                RegionEntry = entry
            };
        }

        public static DragContext ForResizeRegion(Window owner, FrameworkElement _, RegionEntry entry)
        {
            return new DragContext
            {
                Mode = DragMode.ResizeRegion,
                StartPoint = Mouse.GetPosition(owner),
                StartX = entry.Region.X,
                StartY = entry.Region.Y,
                StartWidth = entry.Region.Width,
                StartHeight = entry.Region.Height,
                RegionEntry = entry
            };
        }

        public static DragContext ForMoveAnchor(Window owner, FrameworkElement _, AnchorEntry entry)
        {
            return new DragContext
            {
                Mode = DragMode.MoveAnchor,
                StartPoint = Mouse.GetPosition(owner),
                StartX = entry.Anchor.X,
                StartY = entry.Anchor.Y,
                AnchorEntry = entry
            };
        }
    }

    private enum DragMode
    {
        MoveRegion,
        ResizeRegion,
        MoveAnchor
    }
}
