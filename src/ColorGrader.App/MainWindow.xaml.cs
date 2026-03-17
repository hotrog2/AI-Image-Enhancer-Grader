using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorGrader.App.ViewModels;
using ColorGrader.Core.Models;

namespace ColorGrader.App;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private bool _isDraggingPreview;

    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += async (_, _) =>
        {
            await viewModel.InitializeCommand.ExecuteAsync(null);
            UpdateInteractiveOverlay();
        };
        viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(UpdateInteractiveOverlay);
    }

    private void EnhancedPreviewHost_OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateInteractiveOverlay();

    private void EnhancedPreviewHost_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.EnhancedPreview is null || !TryGetNormalizedPoint(e.GetPosition(EnhancedPreviewHost), out var normalized))
        {
            return;
        }

        _isDraggingPreview = true;
        EnhancedPreviewHost.CaptureMouse();
        ApplyCanvasInteraction(normalized);
        e.Handled = true;
    }

    private void EnhancedPreviewHost_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingPreview || !TryGetNormalizedPoint(e.GetPosition(EnhancedPreviewHost), out var normalized))
        {
            return;
        }

        ApplyCanvasInteraction(normalized);
        e.Handled = true;
    }

    private void EnhancedPreviewHost_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingPreview)
        {
            return;
        }

        _isDraggingPreview = false;
        EnhancedPreviewHost.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void EnhancedPreviewHost_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_viewModel.EnhancedPreview is null)
        {
            return;
        }

        var step = e.Delta > 0 ? 0.03 : -0.03;
        switch (_viewModel.SelectedCanvasEditorTool)
        {
            case CanvasEditorTool.Crop:
                _viewModel.AdjustCropZoom(step);
                break;
            case CanvasEditorTool.LocalizedMask:
                _viewModel.AdjustLocalizedMaskSize(step);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void ApplyCanvasInteraction(Point normalized)
    {
        switch (_viewModel.SelectedCanvasEditorTool)
        {
            case CanvasEditorTool.Crop:
                _viewModel.MoveCropCenterFromCanvas(normalized.X, normalized.Y);
                break;
            case CanvasEditorTool.LocalizedMask:
                if (_viewModel.LocalizedMaskEnabled)
                {
                    _viewModel.MoveLocalizedMaskCenterFromCanvas(normalized.X, normalized.Y);
                }
                break;
        }
    }

    private bool TryGetNormalizedPoint(Point hostPoint, out Point normalized)
    {
        var imageBounds = GetDisplayedImageBounds();
        if (imageBounds.Width <= 0 || imageBounds.Height <= 0 || !imageBounds.Contains(hostPoint))
        {
            normalized = default;
            return false;
        }

        normalized = new Point(
            (hostPoint.X - imageBounds.X) / imageBounds.Width,
            (hostPoint.Y - imageBounds.Y) / imageBounds.Height);
        return true;
    }

    private void UpdateInteractiveOverlay()
    {
        if (!IsLoaded)
        {
            return;
        }

        var imageBounds = GetDisplayedImageBounds();
        if (imageBounds.Width <= 0 || imageBounds.Height <= 0 || _viewModel.EnhancedPreview is null)
        {
            CollapseInteractiveOverlay();
            return;
        }

        UpdateCropOverlay(imageBounds);
        UpdateLocalizedMaskOverlay(imageBounds);
    }

    private void UpdateCropOverlay(Rect imageBounds)
    {
        var cropSettings = new CropStraightenSettings(
            _viewModel.StraightenAngle,
            _viewModel.CropZoom,
            _viewModel.CropOffsetX,
            _viewModel.CropOffsetY);
        var shouldShow = _viewModel.SelectedCanvasEditorTool == CanvasEditorTool.Crop || !cropSettings.IsIdentity;

        if (!shouldShow)
        {
            CropOverlayRectangle.Visibility = Visibility.Collapsed;
            return;
        }

        var cropRectangle = ColorGrader.Imaging.Services.ImageTransformMath.CalculateCropRectangle(
            (int)Math.Round(imageBounds.Width),
            (int)Math.Round(imageBounds.Height),
            cropSettings);

        Canvas.SetLeft(CropOverlayRectangle, imageBounds.X + cropRectangle.X);
        Canvas.SetTop(CropOverlayRectangle, imageBounds.Y + cropRectangle.Y);
        CropOverlayRectangle.Width = cropRectangle.Width;
        CropOverlayRectangle.Height = cropRectangle.Height;
        CropOverlayRectangle.Visibility = Visibility.Visible;
    }

    private void UpdateLocalizedMaskOverlay(Rect imageBounds)
    {
        if (!_viewModel.LocalizedMaskEnabled)
        {
            LocalizedMaskEllipse.Visibility = Visibility.Collapsed;
            LocalizedMaskLine.Visibility = Visibility.Collapsed;
            LocalizedMaskCenterHandle.Visibility = Visibility.Collapsed;
            return;
        }

        var centerX = imageBounds.X + (_viewModel.LocalizedMaskCenterX * imageBounds.Width);
        var centerY = imageBounds.Y + (_viewModel.LocalizedMaskCenterY * imageBounds.Height);

        Canvas.SetLeft(LocalizedMaskCenterHandle, centerX - (LocalizedMaskCenterHandle.Width / 2.0));
        Canvas.SetTop(LocalizedMaskCenterHandle, centerY - (LocalizedMaskCenterHandle.Height / 2.0));
        LocalizedMaskCenterHandle.Visibility = Visibility.Visible;

        if (_viewModel.SelectedLocalizedMaskKind == LocalizedMaskKind.Linear)
        {
            var radians = _viewModel.LocalizedMaskAngle * (Math.PI / 180.0);
            var halfLength = Math.Max(imageBounds.Width, imageBounds.Height) * 0.6;
            var dx = Math.Cos(radians) * halfLength;
            var dy = Math.Sin(radians) * halfLength;

            LocalizedMaskLine.X1 = centerX - dx;
            LocalizedMaskLine.Y1 = centerY - dy;
            LocalizedMaskLine.X2 = centerX + dx;
            LocalizedMaskLine.Y2 = centerY + dy;
            LocalizedMaskLine.Visibility = Visibility.Visible;
            LocalizedMaskEllipse.Visibility = Visibility.Collapsed;
            return;
        }

        var ellipseWidth = _viewModel.LocalizedMaskWidth * imageBounds.Width;
        var ellipseHeight = _viewModel.LocalizedMaskHeight * imageBounds.Height;
        Canvas.SetLeft(LocalizedMaskEllipse, centerX - (ellipseWidth / 2.0));
        Canvas.SetTop(LocalizedMaskEllipse, centerY - (ellipseHeight / 2.0));
        LocalizedMaskEllipse.Width = ellipseWidth;
        LocalizedMaskEllipse.Height = ellipseHeight;
        LocalizedMaskEllipse.Visibility = Visibility.Visible;
        LocalizedMaskLine.Visibility = Visibility.Collapsed;
    }

    private Rect GetDisplayedImageBounds()
    {
        if (EnhancedPreviewImage.Source is not BitmapSource imageSource ||
            EnhancedPreviewHost.ActualWidth <= 0 ||
            EnhancedPreviewHost.ActualHeight <= 0)
        {
            return Rect.Empty;
        }

        var imageAspect = imageSource.Width / imageSource.Height;
        var hostAspect = EnhancedPreviewHost.ActualWidth / EnhancedPreviewHost.ActualHeight;

        if (imageAspect > hostAspect)
        {
            var width = EnhancedPreviewHost.ActualWidth;
            var height = width / imageAspect;
            return new Rect(0, (EnhancedPreviewHost.ActualHeight - height) / 2.0, width, height);
        }

        var finalHeight = EnhancedPreviewHost.ActualHeight;
        var finalWidth = finalHeight * imageAspect;
        return new Rect((EnhancedPreviewHost.ActualWidth - finalWidth) / 2.0, 0, finalWidth, finalHeight);
    }

    private void CollapseInteractiveOverlay()
    {
        CropOverlayRectangle.Visibility = Visibility.Collapsed;
        LocalizedMaskEllipse.Visibility = Visibility.Collapsed;
        LocalizedMaskLine.Visibility = Visibility.Collapsed;
        LocalizedMaskCenterHandle.Visibility = Visibility.Collapsed;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        base.OnClosed(e);
    }
}
