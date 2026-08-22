using System.Windows;

namespace LiteCad.UI;

public static class ToolBarProperties
{
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.RegisterAttached(
            "IsActive",
            typeof(bool),
            typeof(ToolBarProperties),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static void SetIsActive(DependencyObject element, bool value)
        => element.SetValue(IsActiveProperty, value);

    public static bool GetIsActive(DependencyObject element)
        => (bool)element.GetValue(IsActiveProperty);
}
