using LiteCad.Core.Geometry;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.UI.Layout;

public partial class StatusBar : System.Windows.Controls.UserControl
{
    private bool _isTyping;
    private bool _isInputActive;

    public StatusBar()
    {
        InitializeComponent();
    }

    public event EventHandler<double>? LengthCommitted;

    public void SetStatus(string status)
    {
        StatusText.Text = status;
    }

    public void SetCoordinates(PointF point)
    {
        CoordinatesText.Text = $"X: {point.X:F2}  Y: {point.Y:F2}";
    }

    public void SetLength(double? length)
    {
        LengthDisplay.Text = FormatLength(length);
    }

    public void ResetLengthEditing(double? length)
    {
        ClearTyping();
        LengthDisplay.Text = FormatLength(length);
    }

    public void SetArea(double? area)
    {
        AreaText.Text = area.HasValue ? $"A: {area.Value:F2}" : "A: —";
    }

    public void SetLengthInputEnabled(bool enabled)
    {
        if (enabled)
        {
            ActivateLengthInput();
        }
        else
        {
            DeactivateLengthInput();
        }
    }

    public bool ProcessLengthKey(KeyEventArgs e)
    {
        if (!_isInputActive)
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
            CommitLengthInput();
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Escape)
        {
            ClearTyping();
            e.Handled = true;
            return true;
        }

        if (!_isTyping && TryGetKeyChar(e.Key, out _))
        {
            BeginTyping();
            if (!LengthInput.IsKeyboardFocusWithin)
            {
                LengthInput.Focus();
            }
        }

        return false;
    }

    private void ActivateLengthInput()
    {
        _isInputActive = true;
        LengthInput.IsEnabled = true;
        ClearTyping();
        LengthInput.Focus();
    }

    private void DeactivateLengthInput()
    {
        _isInputActive = false;
        _isTyping = false;
        LengthInput.IsEnabled = false;
        LengthInput.Text = string.Empty;
        LengthInput.Background = Brushes.Transparent;
        LengthDisplay.Text = "—";
    }

    private void BeginTyping()
    {
        _isTyping = true;
        LengthInput.Background = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
    }

    private void ClearTyping()
    {
        _isTyping = false;
        LengthInput.Text = string.Empty;
        LengthInput.Background = Brushes.Transparent;
    }

    private void CommitLengthInput()
    {
        if (_isTyping && TryParseLength(LengthInput.Text, out var length))
        {
            LengthCommitted?.Invoke(this, length);
        }

        ClearTyping();
        if (_isInputActive)
        {
            LengthInput.Focus();
        }
    }

    private void LengthInput_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!_isTyping)
        {
            BeginTyping();
            LengthInput.Text = string.Empty;
        }
    }

    private void LengthInput_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (LengthInput.IsEnabled && !string.IsNullOrEmpty(LengthInput.Text))
        {
            _isTyping = true;
            LengthInput.Background = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
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
            CommitLengthInput();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            ClearTyping();
            e.Handled = true;
        }
    }

    private static string FormatLength(double? length)
        => length.HasValue ? length.Value.ToString("F2", CultureInfo.InvariantCulture) : "—";

    private static bool TryParseLength(string text, out double length)
    {
        length = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out length) && length > 0;
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

        return false;
    }
}
