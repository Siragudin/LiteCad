using LiteCad.Core.Document;
using LiteCad.Infrastructure;
using LiteCad.Rendering;
using LiteCad.Rendering.Pdf;
using LiteCad.Resources;
using LiteCad.Services;
using LiteCad.UI;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.UI.Pdf;

public partial class PdfPreviewWindow : Window
{
    private const double PreviewFitMarginDip = 32.0;

    private readonly CadDocument _document;
    private readonly LinearDisplayUnit _linearUnit;
    private readonly Renderer _renderer;
    private readonly string _targetPdfPath;
    private readonly PdfSheet _sheet;
    private PdfRenderResult? _renderResult;
    private bool _isDragging;
    private Point _dragStartContentPoint;
    private Point _dragStartViewPosition;

    public PdfPreviewWindow(
        CadDocument document,
        LinearDisplayUnit linearUnit,
        Renderer renderer,
        string targetPdfPath,
        Window? owner)
    {
        _document = document;
        _linearUnit = linearUnit;
        _renderer = renderer;
        _targetPdfPath = targetPdfPath;
        _sheet = new PdfSheet(document);
        InitializeComponent();
        Background = (Brush)FindResource("CadBackgroundBrush");
        Owner = owner;
        PreviewScrollViewer.SizeChanged += (_, _) => UpdatePreviewViewportFit();
        Loaded += (_, _) => UpdatePreviewViewportFit();
        RefreshPreview();
    }

    internal double GetPreviewSheetDisplayScaleForTesting()
    {
        if (_renderResult is null
            || PreviewViewbox.Width <= 0
            || PreviewViewbox.Height <= 0)
        {
            return 0;
        }

        return PreviewViewbox.Width / _renderResult.Layout.PageWidthDip;
    }

    internal Size PreviewViewportFitSizeForTesting()
        => new(PreviewViewbox.Width, PreviewViewbox.Height);

    public bool Saved { get; private set; }

    internal PdfSheet Sheet => _sheet;

    internal PdfDrawingView PrimaryView => _sheet.PrimaryView;

    internal PdfRenderResult? RenderResult => _renderResult;

    internal void ApplyDragDeltaForTesting(Vector contentDelta)
    {
        PrimaryView.ApplyDragDelta(contentDelta);
        RefreshPreview();
    }

    internal void ApplyWheelZoomForTesting(Point contentPoint, int wheelDelta)
    {
        PrimaryView.ApplyWheelZoomAtContentPoint(contentPoint, wheelDelta);
        RefreshPreview();
    }

    private void OrientationCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OrientationCombo.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        _sheet.Orientation = item.Tag?.ToString() switch
        {
            "Landscape" => PdfPageOrientation.Landscape,
            _ => PdfPageOrientation.Portrait
        };
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        _renderResult = PdfRenderer.Render(_sheet, _linearUnit, _renderer);
        var drawing = VisualTreeHelper.GetDrawing(_renderResult.Visual);
        PreviewImage.Source = drawing is null ? null : new DrawingImage(drawing);
        PreviewImage.Width = _renderResult.Layout.PageWidthDip;
        PreviewImage.Height = _renderResult.Layout.PageHeightDip;
        UpdatePreviewViewportFit();
    }

    private void UpdatePreviewViewportFit()
    {
        if (_renderResult is null)
        {
            return;
        }

        PreviewScrollViewer.UpdateLayout();
        var availableWidth = Math.Max(0, PreviewScrollViewer.ViewportWidth - PreviewFitMarginDip);
        var availableHeight = Math.Max(0, PreviewScrollViewer.ViewportHeight - PreviewFitMarginDip);
        if (availableWidth <= 1 || availableHeight <= 1)
        {
            return;
        }

        var pageWidth = _renderResult.Layout.PageWidthDip;
        var pageHeight = _renderResult.Layout.PageHeightDip;
        var scale = Math.Min(availableWidth / pageWidth, availableHeight / pageHeight);
        PreviewViewbox.Width = pageWidth * scale;
        PreviewViewbox.Height = pageHeight * scale;
    }

    private void PreviewImage_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_renderResult is null || !TryGetContentPoint(e, out var contentPoint))
        {
            return;
        }

        if (!IsPointOverDrawing(contentPoint))
        {
            return;
        }

        _isDragging = true;
        _dragStartContentPoint = contentPoint;
        _dragStartViewPosition = PrimaryView.Position;
        PreviewImage.CaptureMouse();
        PreviewImage.Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void PreviewImage_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_renderResult is null || !TryGetContentPoint(e, out var contentPoint))
        {
            return;
        }

        if (_isDragging)
        {
            var delta = contentPoint - _dragStartContentPoint;
            PrimaryView.Position = new Point(
                _dragStartViewPosition.X + delta.X,
                _dragStartViewPosition.Y + delta.Y);
            RefreshPreview();
            e.Handled = true;
            return;
        }

        PreviewImage.Cursor = IsPointOverDrawing(contentPoint) ? Cursors.SizeAll : Cursors.Arrow;
    }

    private void PreviewImage_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        PreviewImage.ReleaseMouseCapture();
        PreviewImage.Cursor = Cursors.Arrow;
        e.Handled = true;
    }

    private void PreviewImage_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            return;
        }

        PreviewImage.Cursor = Cursors.Arrow;
    }

    private void PreviewScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_renderResult is null || !TryGetContentPoint(e, out var contentPoint))
        {
            return;
        }

        if (!IsPointOverDrawing(contentPoint))
        {
            return;
        }

        PrimaryView.ApplyWheelZoomAtContentPoint(contentPoint, e.Delta);
        RefreshPreview();
        e.Handled = true;
    }

    private bool TryGetContentPoint(MouseEventArgs e, out Point contentPoint)
    {
        contentPoint = default;
        if (_renderResult is null)
        {
            return false;
        }

        var pagePoint = e.GetPosition(PreviewImage);
        contentPoint = new Point(
            pagePoint.X - _renderResult.Layout.MarginDip,
            pagePoint.Y - _renderResult.Layout.MarginDip);
        return true;
    }

    private bool IsPointOverDrawing(Point contentPoint)
    {
        if (_renderResult is null)
        {
            return false;
        }

        var drawingBounds = _renderResult.Layout.GetDrawingBoundsInContent(PrimaryView);
        return drawingBounds.Contains(contentPoint);
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_renderResult is null)
        {
            return;
        }

        var targetPath = PdfFileNameHelper.NormalizePdfFilePath(_targetPdfPath);
        if (File.Exists(targetPath)
            && MessageBox.Show(
                Strings.Dialog_OverwritePdf_Message,
                Strings.Dialog_OverwritePdf_Title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _renderResult = PdfRenderer.Render(_sheet, _linearUnit, _renderer);
            PdfRenderer.Save(_renderResult, _linearUnit, targetPath);
            Saved = true;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format(CultureInfo.CurrentCulture, Strings.Dialog_PdfExportFailed_Message, ex.Message),
                Strings.Dialog_PdfExportFailed_Title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public static bool TryShowAndSave(
        CadDocument document,
        LinearDisplayUnit linearUnit,
        Renderer renderer,
        string targetPdfPath,
        Window owner)
    {
        var window = new PdfPreviewWindow(document, linearUnit, renderer, targetPdfPath, owner);
        return window.ShowDialog() == true && window.Saved;
    }
}
