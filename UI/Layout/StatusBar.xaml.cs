using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Resources;
using LiteCad.Services;
using LiteCad.UI;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.UI.Layout;

public partial class StatusBar : System.Windows.Controls.UserControl
{
    private static readonly object InitializeComponentLock = new();

    private DisplayUnitSettings? _displayUnitSettings;
    private Action? _onDisplayUnitChanged;
    private bool _suppressUnitComboEvents;
    private double? _lastLength;
    private double? _lastArea;
    private PointF? _lastCoordinates;
    private double? _lastWidthPreview;
    private double? _lastHeightPreview;
    private bool _isTyping;
    private bool _isInputActive;
    private bool _isRectangleInputActive;
    private bool _isTypingWidth;
    private bool _isTypingHeight;
    private RectangleSizeField _activeRectangleField = RectangleSizeField.Width;
    private DualFieldLabelMode _dualFieldLabelMode = DualFieldLabelMode.WidthHeight;
    private LineInputLabelMode _lineInputLabelMode = LineInputLabelMode.Length;

    public StatusBar()
    {
        lock (InitializeComponentLock)
        {
            InitializeComponent();
        }
    }

    public event EventHandler<string>? LengthCommitted;

    public event EventHandler<(string Width, string Height)>? RectangleSizeCommitted;

    public Func<string, bool>? TryCommitLengthInput { get; set; }

    public Func<(string Width, string Height), bool>? TryCommitRectangleSizeInput { get; set; }

    public bool IsRectangleInputActive => _isRectangleInputActive;

    public bool IsLineInputActive => _isInputActive && !_isRectangleInputActive;

    public RectangleSizeField ActiveRectangleField => _activeRectangleField;

    public DualFieldLabelMode DualFieldLabels => _dualFieldLabelMode;

    public LineInputLabelMode LineInputLabelMode => _lineInputLabelMode;

    public string FirstFieldLabelText => FirstFieldLabel.Text;

    public string LineInputLabelText => LineInputLabel.Text;

    public string RectangleWidthText => WidthInput.Text;

    public string RectangleHeightText => HeightInput.Text;

    public string LineInputText => LengthInput.Text;

    public bool IsWidthFieldFocused => WidthInput.IsKeyboardFocusWithin;

    public bool IsHeightFieldFocused => HeightInput.IsKeyboardFocusWithin;

    public bool IsAngularInputMode => _lineInputLabelMode == LineInputLabelMode.Angle;

    public LinearDisplayUnit LinearDisplayUnit
        => _displayUnitSettings?.LinearUnit ?? LinearDisplayUnit.Millimeters;

    public void BindSession(CadSession session, Action? onDisplayUnitChanged = null)
    {
        if (_displayUnitSettings is not null)
        {
            _displayUnitSettings.Changed -= OnDisplayUnitSettingsChanged;
        }

        _displayUnitSettings = session.DisplayUnitSettings;
        _onDisplayUnitChanged = onDisplayUnitChanged;
        _displayUnitSettings.Changed += OnDisplayUnitSettingsChanged;
        SyncLinearUnitComboFromSettings();
    }

    public void SetDualFieldLabelMode(DualFieldLabelMode mode)
    {
        _dualFieldLabelMode = mode;
        switch (mode)
        {
            case DualFieldLabelMode.MoveOffset:
                FirstFieldLabel.Text = Strings.Label_X;
                SecondFieldLabel.Text = Strings.Label_Y;
                break;
            default:
                FirstFieldLabel.Text = Strings.Label_Width;
                SecondFieldLabel.Text = Strings.Label_Height;
                break;
        }
    }

    public void SetLineInputText(string text)
    {
        LengthInput.Text = text;
        if (!string.IsNullOrEmpty(text))
        {
            _isTyping = true;
            LengthInput.Background = TypingBackgroundBrush;
        }
    }

    public (string First, string Second) GetDualFieldInputText()
        => (WidthInput.Text, HeightInput.Text);

    public void SetDualFieldInputText(string first, string second)
    {
        SetRectangleWidthInputText(first);
        SetRectangleHeightInputText(second);
    }

    public void SetRectangleWidthInputText(string text)
    {
        WidthInput.Text = text;
        if (!string.IsNullOrEmpty(text))
        {
            _isTypingWidth = true;
            WidthInput.Background = TypingBackgroundBrush;
        }
    }

    public void SetRectangleHeightInputText(string text)
    {
        HeightInput.Text = text;
        if (!string.IsNullOrEmpty(text))
        {
            _isTypingHeight = true;
            HeightInput.Background = TypingBackgroundBrush;
        }
    }

    public void SetStatus(string status)
    {
        StatusText.Text = status;
    }

    public void SetCoordinates(PointF point)
    {
        _lastCoordinates = point;
        var unit = LinearDisplayUnit;
        CoordinatesText.Text = Strings.Format(
            Strings.Format_Coordinates,
            UnitDisplayFormatter.FormatCoordinate(point.X, unit),
            UnitDisplayFormatter.FormatCoordinate(point.Y, unit));
    }

    public void SetLengthText(string? text)
    {
        LengthDisplay.Text = string.IsNullOrWhiteSpace(text) ? Strings.Label_EmptyValue : text;
    }

    public void SetLength(double? length)
    {
        _lastLength = length;
        LengthDisplay.Text = FormatLengthInstance(length);
    }

    public void ResetLengthEditing(double? length)
    {
        ClearLineTyping();
        _lastLength = length;
        LengthDisplay.Text = FormatLengthInstance(length);
    }

    public void SetArea(double? area)
    {
        _lastArea = area;
        AreaText.Text = area.HasValue
            ? Strings.Format(Strings.Format_Area, UnitDisplayFormatter.FormatArea(area.Value))
            : Strings.Format_AreaEmpty;
    }

    public void SetLengthInputEnabled(bool enabled, LineInputLabelMode mode = LineInputLabelMode.Length)
    {
        if (enabled)
        {
            ActivateLineInput(mode);
        }
        else
        {
            DeactivateLineInput();
        }
    }

    public void SetRectangleSizeInputEnabled(bool enabled, DualFieldLabelMode labelMode = DualFieldLabelMode.WidthHeight)
    {
        if (enabled)
        {
            ActivateRectangleSizeInput(labelMode);
        }
        else
        {
            DeactivateRectangleSizeInput();
        }
    }

    public void SetRectangleSizePreview(double? width, double? height)
    {
        if (!_isRectangleInputActive)
        {
            return;
        }

        _lastWidthPreview = width;
        _lastHeightPreview = height;

        if (!_isTypingWidth)
        {
            WidthDisplay.Text = FormatLengthInstance(width);
        }

        if (!_isTypingHeight)
        {
            HeightDisplay.Text = FormatLengthInstance(height);
        }
    }

    public void ResetRectangleSizeInput(double? width, double? height)
    {
        _lastWidthPreview = width;
        _lastHeightPreview = height;
        ClearRectangleTyping();
        WidthDisplay.Text = FormatLengthInstance(width);
        HeightDisplay.Text = FormatLengthInstance(height);
        WidthInput.Text = string.Empty;
        HeightInput.Text = string.Empty;
    }

    public bool ToggleRectangleSizeField()
    {
        if (!_isRectangleInputActive)
        {
            return false;
        }

        _activeRectangleField = _activeRectangleField == RectangleSizeField.Width
            ? RectangleSizeField.Height
            : RectangleSizeField.Width;

        UpdateRectangleFieldHighlight();
        FocusActiveRectangleField();
        return true;
    }

    public bool ProcessLengthKey(KeyEventArgs e)
    {
        if (!_isInputActive || _isRectangleInputActive)
        {
            return false;
        }

        if (e.Key == Key.Tab)
        {
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Enter)
        {
            CommitLineInput();
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Escape)
        {
            ClearLineTyping();
            e.Handled = true;
            return true;
        }

        if (!_isTyping && TryGetKeyChar(e.Key, out _))
        {
            BeginLineTyping();
            if (!LengthInput.IsKeyboardFocusWithin)
            {
                LengthInput.Focus();
            }
        }

        return false;
    }

    public bool ProcessRectangleSizeKey(KeyEventArgs e)
    {
        if (!_isRectangleInputActive)
        {
            return false;
        }

        if (IsAltToggleKey(e))
        {
            ToggleRectangleSizeField();
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Tab)
        {
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Enter)
        {
            CommitRectangleSizeInput();
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Escape)
        {
            return false;
        }

        if (TryGetKeyChar(e.Key, out _))
        {
            BeginRectangleTyping(_activeRectangleField);
            FocusActiveRectangleField();
        }

        return false;
    }

    public static bool IsAltToggleKey(KeyEventArgs e)
        => e.Key is Key.LeftAlt or Key.RightAlt
           || (e.Key == Key.System && e.SystemKey is Key.LeftAlt or Key.RightAlt);

    private void ActivateLineInput(LineInputLabelMode mode = LineInputLabelMode.Length)
    {
        DeactivateRectangleSizeInput();
        _isInputActive = true;
        _lineInputLabelMode = mode;
        LineInputPanel.Visibility = Visibility.Visible;
        LineInputLabel.Text = GetLineInputLabelText(mode);
        LengthInput.IsEnabled = true;
        ClearLineTyping();
        LengthInput.Focus();
    }

    private void DeactivateLineInput()
    {
        _isInputActive = false;
        _isTyping = false;
        _lineInputLabelMode = LineInputLabelMode.Length;
        LengthInput.IsEnabled = false;
        LengthInput.Text = string.Empty;
        LengthInput.Background = Brushes.Transparent;
        LengthDisplay.Text = Strings.Label_EmptyValue;
        LineInputLabel.Text = Strings.Label_Length;
        if (!_isRectangleInputActive)
        {
            LineInputPanel.Visibility = Visibility.Visible;
        }
    }

    private void ActivateRectangleSizeInput(DualFieldLabelMode labelMode = DualFieldLabelMode.WidthHeight)
    {
        DeactivateLineInput();
        _isRectangleInputActive = true;
        _activeRectangleField = RectangleSizeField.Width;
        SetDualFieldLabelMode(labelMode);
        LineInputPanel.Visibility = Visibility.Collapsed;
        RectangleInputPanel.Visibility = Visibility.Visible;
        WidthInput.IsEnabled = true;
        HeightInput.IsEnabled = true;
        ClearRectangleTyping();
        WidthDisplay.Text = Strings.Label_EmptyValue;
        HeightDisplay.Text = Strings.Label_EmptyValue;
        UpdateRectangleFieldHighlight();
        FocusActiveRectangleField();
    }

    private void DeactivateRectangleSizeInput()
    {
        _isRectangleInputActive = false;
        _isTypingWidth = false;
        _isTypingHeight = false;
        WidthInput.IsEnabled = false;
        HeightInput.IsEnabled = false;
        WidthInput.Text = string.Empty;
        HeightInput.Text = string.Empty;
        WidthInput.Background = Brushes.Transparent;
        HeightInput.Background = Brushes.Transparent;
        WidthDisplay.Text = Strings.Label_EmptyValue;
        HeightDisplay.Text = Strings.Label_EmptyValue;
        RectangleInputPanel.Visibility = Visibility.Collapsed;
        LineInputPanel.Visibility = Visibility.Visible;
        SetDualFieldLabelMode(DualFieldLabelMode.WidthHeight);
        UpdateRectangleFieldHighlight();
    }

    private void FocusActiveRectangleField()
    {
        if (_activeRectangleField == RectangleSizeField.Width)
        {
            WidthInput.Focus();
            return;
        }

        HeightInput.Focus();
    }

    private void UpdateRectangleFieldHighlight()
    {
        if (!_isRectangleInputActive)
        {
            WidthInputBorder.BorderBrush = InactiveFieldBorderBrush;
            HeightInputBorder.BorderBrush = InactiveFieldBorderBrush;
            return;
        }

        WidthInputBorder.BorderBrush = _activeRectangleField == RectangleSizeField.Width
            ? ActiveFieldBorderBrush
            : InactiveFieldBorderBrush;
        HeightInputBorder.BorderBrush = _activeRectangleField == RectangleSizeField.Height
            ? ActiveFieldBorderBrush
            : InactiveFieldBorderBrush;
    }

    private void BeginLineTyping()
    {
        _isTyping = true;
        LengthInput.Background = TypingBackgroundBrush;
    }

    private void ClearLineTyping()
    {
        _isTyping = false;
        LengthInput.Text = string.Empty;
        LengthInput.Background = Brushes.Transparent;
    }

    private void BeginRectangleTyping(RectangleSizeField field)
    {
        if (field == RectangleSizeField.Width)
        {
            _isTypingWidth = true;
            WidthInput.Background = TypingBackgroundBrush;
            return;
        }

        _isTypingHeight = true;
        HeightInput.Background = TypingBackgroundBrush;
    }

    private void ClearRectangleTyping()
    {
        _isTypingWidth = false;
        _isTypingHeight = false;
        WidthInput.Text = string.Empty;
        HeightInput.Text = string.Empty;
        WidthInput.Background = Brushes.Transparent;
        HeightInput.Background = Brushes.Transparent;
    }

    private void CommitLineInput()
    {
        if (_isTyping && !string.IsNullOrWhiteSpace(LengthInput.Text))
        {
            var text = LengthInput.Text;
            var committed = TryCommitLengthInput?.Invoke(text);
            if (TryCommitLengthInput is null)
            {
                LengthCommitted?.Invoke(this, text);
                committed = true;
            }

            if (committed == true || !PreservesInputOnFailedCommit())
            {
                ClearLineTyping();
            }
        }
        else
        {
            ClearLineTyping();
        }

        if (_isInputActive)
        {
            LengthInput.Focus();
        }
    }

    private void CommitRectangleSizeInput()
    {
        var sizes = (WidthInput.Text, HeightInput.Text);
        var committed = TryCommitRectangleSizeInput?.Invoke(sizes);
        if (TryCommitRectangleSizeInput is null)
        {
            RectangleSizeCommitted?.Invoke(this, sizes);
            committed = true;
        }

        if (committed == true || _dualFieldLabelMode != DualFieldLabelMode.MoveOffset)
        {
            ClearRectangleTyping();
        }

        if (_isRectangleInputActive)
        {
            FocusActiveRectangleField();
        }
    }

    public void RefreshThemeStyles()
    {
        UpdateRectangleFieldHighlight();
        if (_isTyping)
        {
            LengthInput.Background = TypingBackgroundBrush;
        }

        if (_isTypingWidth)
        {
            WidthInput.Background = TypingBackgroundBrush;
        }

        if (_isTypingHeight)
        {
            HeightInput.Background = TypingBackgroundBrush;
        }
    }

    public void RefreshLocalizedLabels()
    {
        if (_isRectangleInputActive)
        {
            SetDualFieldLabelMode(_dualFieldLabelMode);
            return;
        }

        if (_isInputActive)
        {
            LineInputLabel.Text = GetLineInputLabelText(_lineInputLabelMode);
        }
    }

    private bool PreservesInputOnFailedCommit()
        => _lineInputLabelMode is LineInputLabelMode.Distance
            or LineInputLabelMode.Radius
            or LineInputLabelMode.ArcHeight
            or LineInputLabelMode.Angle
            or LineInputLabelMode.Offset
            or LineInputLabelMode.Text;

    private bool IsMoveDistanceInput()
        => _isInputActive && _lineInputLabelMode == LineInputLabelMode.Distance;

    public bool IsMoveDistanceInputMode()
        => IsMoveDistanceInput();

    private static string GetLineInputLabelText(LineInputLabelMode mode)
        => mode switch
        {
            LineInputLabelMode.Distance => Strings.Label_Distance,
            LineInputLabelMode.Radius => Strings.Label_Radius,
            LineInputLabelMode.ArcHeight => Strings.Label_ArcHeight,
            LineInputLabelMode.Angle => Strings.Label_Angle,
            LineInputLabelMode.Offset => Strings.Label_Offset,
            LineInputLabelMode.Text => Strings.Label_LeaderText,
            _ => Strings.Label_Length
        };

    private void LengthInput_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!_isTyping)
        {
            BeginLineTyping();
            LengthInput.Text = string.Empty;
        }
    }

    private void LengthInput_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (LengthInput.IsEnabled && !string.IsNullOrEmpty(LengthInput.Text))
        {
            _isTyping = true;
            LengthInput.Background = TypingBackgroundBrush;
        }
        else if (!_isTyping)
        {
            LengthInput.Background = Brushes.Transparent;
        }
    }

    private void LengthInput_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitLineInput();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            ClearLineTyping();
            e.Handled = true;
        }
    }

    private void WidthInput_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        _activeRectangleField = RectangleSizeField.Width;
        UpdateRectangleFieldHighlight();
        if (!_isTypingWidth)
        {
            BeginRectangleTyping(RectangleSizeField.Width);
            WidthInput.Text = string.Empty;
        }
    }

    private void HeightInput_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        _activeRectangleField = RectangleSizeField.Height;
        UpdateRectangleFieldHighlight();
        if (!_isTypingHeight)
        {
            BeginRectangleTyping(RectangleSizeField.Height);
            HeightInput.Text = string.Empty;
        }
    }

    private void WidthInput_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (WidthInput.IsEnabled && !string.IsNullOrEmpty(WidthInput.Text))
        {
            _isTypingWidth = true;
            WidthInput.Background = TypingBackgroundBrush;
        }
        else if (!_isTypingWidth)
        {
            WidthInput.Background = Brushes.Transparent;
        }
    }

    private void HeightInput_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (HeightInput.IsEnabled && !string.IsNullOrEmpty(HeightInput.Text))
        {
            _isTypingHeight = true;
            HeightInput.Background = TypingBackgroundBrush;
        }
        else if (!_isTypingHeight)
        {
            HeightInput.Background = Brushes.Transparent;
        }
    }

    private void WidthInput_OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (!_isRectangleInputActive)
        {
            return;
        }

        _activeRectangleField = RectangleSizeField.Width;
        UpdateRectangleFieldHighlight();
    }

    private void HeightInput_OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (!_isRectangleInputActive)
        {
            return;
        }

        _activeRectangleField = RectangleSizeField.Height;
        UpdateRectangleFieldHighlight();
    }

    private void RectangleFieldInput_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (IsAltToggleKey(e))
        {
            ToggleRectangleSizeField();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitRectangleSizeInput();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = false;
        }
    }

    private string FormatLengthInstance(double? length)
        => length.HasValue
            ? UnitDisplayFormatter.FormatLinear(length.Value, LinearDisplayUnit)
            : Strings.Label_EmptyValue;

    private void OnDisplayUnitSettingsChanged()
    {
        SyncLinearUnitComboFromSettings();
        RefreshDisplayedValues();
        _onDisplayUnitChanged?.Invoke();
    }

    private void RefreshDisplayedValues()
    {
        if (_lastCoordinates is PointF coordinates)
        {
            SetCoordinates(coordinates);
        }

        if (_lastLength.HasValue)
        {
            SetLength(_lastLength);
        }

        if (_lastArea.HasValue)
        {
            SetArea(_lastArea);
        }

        if (_isRectangleInputActive)
        {
            SetRectangleSizePreview(_lastWidthPreview, _lastHeightPreview);
        }
    }

    private void SyncLinearUnitComboFromSettings()
    {
        if (_displayUnitSettings is null)
        {
            return;
        }

        _suppressUnitComboEvents = true;
        foreach (ComboBoxItem item in LinearUnitCombo.Items)
        {
            var isMatch = item.Tag?.ToString() switch
            {
                "Meters" => _displayUnitSettings.LinearUnit == LinearDisplayUnit.Meters,
                _ => _displayUnitSettings.LinearUnit == LinearDisplayUnit.Millimeters
            };

            if (isMatch)
            {
                LinearUnitCombo.SelectedItem = item;
                break;
            }
        }

        _suppressUnitComboEvents = false;
    }

    private void LinearUnitCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUnitComboEvents || _displayUnitSettings is null || LinearUnitCombo.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        _displayUnitSettings.LinearUnit = item.Tag?.ToString() switch
        {
            "Meters" => LinearDisplayUnit.Meters,
            _ => LinearDisplayUnit.Millimeters
        };
    }

    private static bool TryGetKeyChar(Key key, out char character)
    {
        character = '\0';
        if (key is >= Key.D0 and <= Key.D9)
        {
            character = (char)('0' + (key - Key.D0));
            return true;
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            character = (char)('0' + (key - Key.NumPad0));
            return true;
        }

        if (key is Key.OemPeriod or Key.Decimal)
        {
            character = '.';
            return true;
        }

        if (key is Key.OemComma)
        {
            character = ',';
            return true;
        }

        if (key is Key.X)
        {
            character = 'x';
            return true;
        }

        if (key is Key.Oem1 or Key.Oem102)
        {
            character = '×';
            return true;
        }

        return false;
    }

    private SolidColorBrush ActiveFieldBorderBrush => (SolidColorBrush)FindResource("CadAccentBrush");

    private SolidColorBrush InactiveFieldBorderBrush => (SolidColorBrush)FindResource("CadBorderBrush");

    private SolidColorBrush TypingBackgroundBrush => (SolidColorBrush)FindResource("CadInputActiveBrush");
}
