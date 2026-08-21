using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Resources;
using LiteCad.UI.Layout;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Xunit;

namespace LiteCad.Tests;

public class LanguageSelectorTests : IDisposable
{
    private readonly string _tempSettingsDirectory;

    public LanguageSelectorTests()
    {
        _tempSettingsDirectory = Path.Combine(Path.GetTempPath(), "LiteCad.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempSettingsDirectory);
        LanguageSettingsStore.SetSettingsDirectoryForTests(_tempSettingsDirectory);
        LocalizationManager.Instance.Initialize(AppLanguage.English);
    }

    public void Dispose()
    {
        LanguageSettingsStore.SetSettingsDirectoryForTests(null);
        if (Directory.Exists(_tempSettingsDirectory))
        {
            Directory.Delete(_tempSettingsDirectory, recursive: true);
        }
    }

    [Fact]
    public void DefaultLanguage_IsEnglish()
    {
        Assert.Equal(AppLanguage.English, LocalizationManager.Instance.Language);
        Assert.Equal(AppLanguage.English, LanguageSettingsStore.LoadLanguage());
    }

    [Fact]
    public void SelectingRussian_SavesRussian()
    {
        LocalizationManager.Instance.SetLanguage(AppLanguage.Russian);

        Assert.Equal(AppLanguage.Russian, LocalizationManager.Instance.Language);
        Assert.Equal(AppLanguage.Russian, LanguageSettingsStore.LoadLanguage());
    }

    [Fact]
    public void NextStartup_RestoresRussian()
    {
        LocalizationManager.Instance.SetLanguage(AppLanguage.Russian);

        LocalizationManager.Instance.Initialize(LanguageSettingsStore.LoadLanguage());

        Assert.Equal(AppLanguage.Russian, LocalizationManager.Instance.Language);
    }

    [Fact]
    public void SelectingEnglish_SavesEnglish()
    {
        LocalizationManager.Instance.SetLanguage(AppLanguage.Russian);
        LocalizationManager.Instance.SetLanguage(AppLanguage.English);

        Assert.Equal(AppLanguage.English, LocalizationManager.Instance.Language);
        Assert.Equal(AppLanguage.English, LanguageSettingsStore.LoadLanguage());
    }

    [Fact]
    public void Language_IsNotPartOfCadDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(1, 0));

        LocalizationManager.Instance.SetLanguage(AppLanguage.Russian);

        Assert.Equal(2, document.Vertices.Count);
        Assert.DoesNotContain(document.GetType().GetProperties(), property => property.Name == "Language");
    }

    [Fact]
    public void SwitchingLanguage_DoesNotModifyTopology()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(1, 0));
        TestDocumentHelpers.AddEdge(document, new PointF(1, 0), new PointF(1, 1));
        PolygonBuilder.SyncFaces(document, MathUtils.DefaultTolerance);
        var verticesBefore = document.Vertices.Count;
        var edgesBefore = document.Edges.Count;
        var polygonsBefore = document.Polygons.Count;

        LocalizationManager.Instance.SetLanguage(AppLanguage.Russian);
        LocalizationManager.Instance.SetLanguage(AppLanguage.English);

        Assert.Equal(verticesBefore, document.Vertices.Count);
        Assert.Equal(edgesBefore, document.Edges.Count);
        Assert.Equal(polygonsBefore, document.Polygons.Count);
    }

    [Fact]
    public void Popup_ClosesAfterSelection()
    {
        RunSta(() =>
        {
            using var host = CreateSelectorHost();
            host.Selector.UpdatePopupState(true);
            Assert.True(host.Selector.IsPopupOpen);

            host.Selector.SelectLanguageOption(AppLanguage.Russian);

            Assert.False(host.Selector.IsPopupOpen);
            Assert.Equal(AppLanguage.Russian, LocalizationManager.Instance.Language);
        });
    }

    [Fact]
    public void Selector_DisplaysCurrentLanguageCode()
    {
        RunSta(() =>
        {
            using var host = CreateSelectorHost();
            LocalizationManager.Instance.SetLanguage(AppLanguage.English, persist: false);
            Assert.Contains("EN", host.CurrentLabelText);

            LocalizationManager.Instance.SetLanguage(AppLanguage.Russian, persist: false);
            Assert.Contains("RU", host.CurrentLabelText);
        });
    }

    private static SelectorHost CreateSelectorHost()
        => new();

    private static void RunSta(Action action) => WpfTestUtilities.RunSta(action);

    private sealed class SelectorHost : IDisposable
    {
        private readonly Window _window;

        public SelectorHost()
        {
            Selector = new LanguageSelector();
            _window = new Window
            {
                Content = Selector,
                Width = 120,
                Height = 80,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Visibility = Visibility.Hidden
            };
            _window.Show();
            _window.UpdateLayout();
        }

        public LanguageSelector Selector { get; }

        public string CurrentLabelText
            => FindVisualChildren<TextBlock>(Selector)
                .First(block => block.Text is "EN" or "RU" or { Length: > 0 })
                .Text;

        public void SelectLanguage(string language)
        {
            Selector.UpdatePopupState(true);
            Selector.UpdateLayout();

            var button = FindVisualChildren<Button>(Selector)
                .First(item => string.Equals(item.Tag as string, language, StringComparison.OrdinalIgnoreCase));
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Selector.UpdateLayout();
        }

        public void Dispose()
            => _window.Close();

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
            where T : DependencyObject
        {
            var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (var nested in FindVisualChildren<T>(child))
                {
                    yield return nested;
                }
            }
        }
    }
}
