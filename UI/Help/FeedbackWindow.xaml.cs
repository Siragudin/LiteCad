using LiteCad.Resources;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;

namespace LiteCad.UI.Help;

public partial class FeedbackWindow : Window
{
    public FeedbackWindow()
    {
        InitializeComponent();
        BotHandleRun.Text = Strings.Help_Feedback_Handle;
    }

    private void QrImage_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => OpenBotLink();

    private void BotLink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        OpenBotLink();
        e.Handled = true;
    }

    private void OpenBotLink()
    {
        var url = Strings.Help_Feedback_Url;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            MessageBox.Show(
                this,
                string.Format(Strings.Help_Feedback_Failed, Environment.NewLine, url),
                Strings.Dialog_Feedback_Title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
