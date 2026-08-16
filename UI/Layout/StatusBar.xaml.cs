using LiteCad.Core.Geometry;
using LiteCad.Services;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.UI.Layout;

public partial class StatusBar : System.Windows.Controls.UserControl
{
    private static readonly SolidColorBrush ActiveFieldBorderBrush = new(Color.FromRgb(0x21, 0x96, 0xF3));
    private static readonly SolidColorBrush InactiveFieldBorderBrush = new(Color.FromRgb(0xCC, 0xCC, 0xCC));
    private static readonly SolidColorBrush TypingBackgroundBrush = new(Color.FromArgb(220, 255, 255, 255));

    private bool _isTyping;
    private bool _isInputActive;
    private bool _isRectangleInputActive;
    private bool _isTypingWidth;
    private bool _isTypingHeight;
    private RectangleSizeField _activeRectangleField = RectangleSizeField.Width;
    private DualFieldLabelMode _dualFieldLabelMode = DualFieldLabelMode.WidthHeight;

    public StatusBar()
    {
        InitializeComponent();
    }

    public event EventHandler<string>? LengthCommitted;

    public event EventHandler<(string Width, string Height)>? RectangleSizeCommitted;

    public Func<string, bool>? TryCommitLengthInput { get; set; }

    public Func<(string Width, string Height), bool>? TryCommitRectangleSizeInput { get; set; }

    public bool IsRectangleInputActive => _isRectangleInputActive;

    public bool IsLineInputActive => _isInputActive && !_isRectangleInputActive;

    public RectangleSizeField ActiveRectangleField => _activeRectangleField;

    public DualFieldLabelMode DualFieldLabels => _dualFieldLabelMode;

    public string FirstFieldLabelText => FirstFieldLabel.Text;

    public string LineInputLabelText => LineInputLabel.Text;

    public string RectangleWidthText => WidthInput.Text;

    public string RectangleHeightText => HeightInput.Text;

    public string LineInputText => LengthInput.Text;

    public bool IsWidthFieldFocused => WidthInput.IsKeyboardFocusWithin;

    public bool IsHeightFieldFocused => HeightInput.IsKeyboardFocusWithin;

    public void SetDualFieldLabelMode(DualFieldLabelMode mode)
    {
        _dualFieldLabelMode = mode;
        switch (mode)
        {
            case DualFieldLabelMode.MoveOffset:
                FirstFieldLabel.Text = "X:";
                SecondFieldLabel.Text = "Y:";
                break;
            default:
                FirstFieldLabel.Text = "W:";
                SecondFieldLabel.Text = "H:";
                break;
        }
    }

    public void SetLineInputLabel(string label)
    {
        LineInputLabel.Text = label;
    }

    public void SetDualFieldInputText(string first, string second)
    {
        SetRectangleWidthInputText(first);
        SetRectangleHeightInputText(second);
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
        CoordinatesText.Text = $"X: {point.X:F2}  Y: {point.Y:F2}";
    }

    public void SetLengthText(string? text)
    {
        LengthDisplay.Text = string.IsNullOrWhiteSpace(text) ? "—" : text;
    }

    public void SetLength(double? length)
    {
        LengthDisplay.Text = FormatLength(length);
    }

    public void ResetLengthEditing(double? length)
    {
        ClearLineTyping();
        LengthDisplay.Text = FormatLength(length);
    }

    public void SetArea(double? area)
    {
        AreaText.Text = area.HasValue ? $"A: {area.Value:F2}" : "A: —";
    }

    public void SetLengthInputEnabled(bool enabled, string label = "L:")
    {
        if (enabled)
        {
            ActivateLineInput(label);
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

        if (!_isTypingWidth)
        {
            WidthDisplay.Text = FormatLength(width);
        }

        if (!_isTypingHeight)
        {
            HeightDisplay.Text = FormatLength(height);
        }
    }

    public void ResetRectangleSizeInput(double? width, double? height)
    {
        ClearRectangleTyping();
        WidthDisplay.Text = FormatLength(width);
        HeightDisplay.Text = FormatLength(height);
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

    private void ActivateLineInput(string label = "L:")
    {
        DeactivateRectangleSizeInput();
        _isInputActive = true;
        LineInputPanel.Visibility = Visibility.Visible;
        LineInputLabel.Text = label;
        LengthInput.IsEnabled = true;
        ClearLineTyping();
        LengthInput.Focus();
    }

    private void DeactivateLineInput()
    {
        _isInputActive = false;
        _isTyping = false;
        LengthInput.IsEnabled = false;
        LengthInput.Text = string.Empty;
        LengthInput.Background = Brushes.Transparent;
        LengthDisplay.Text = "—";
        LineInputLabel.Text = "L:";
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
        WidthDisplay.Text = "—";
        HeightDisplay.Text = "—";
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
        WidthDisplay.Text = "—";
        HeightDisplay.Text = "—";
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

            if (committed == true || !IsMoveDistanceInput())
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

    private bool IsMoveDistanceInput()
        => _isInputActive && LineInputLabel.Text == "Distance:";

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

    private static string FormatLength(double? length)
        => length.HasValue ? length.Value.ToString("F2", CultureInfo.InvariantCulture) : "—";

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
}
