using LiteCad.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace LiteCad.UI.Layout;

public partial class LanguageSelector : UserControl
{
    public LanguageSelector()
    {
        InitializeComponent();
        LanguagePopup.Closed += (_, _) => UpdatePopupState(false);
    }

    public bool IsPopupOpen => LanguagePopup.IsOpen;

    private void LanguageToggle_OnClick(object sender, RoutedEventArgs e)
    {
        LanguagePopup.IsOpen = LanguageToggle.IsChecked == true;
    }

    private void LanguageOption_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string language })
        {
            return;
        }

        LocalizationManager.Instance.SetLanguage(language);
        UpdatePopupState(false);
    }

    public void SelectLanguageOption(string language)
    {
        LocalizationManager.Instance.SetLanguage(language);
        UpdatePopupState(false);
    }

    public void UpdatePopupState(bool isOpen)
    {
        LanguagePopup.IsOpen = isOpen;
        LanguageToggle.IsChecked = isOpen;
    }
}
