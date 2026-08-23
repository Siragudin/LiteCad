using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Services;
using LiteCad.Tools;
using LiteCad.UI.Layout;
using LiteCad.UI.Pdf;
using System.IO;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Xunit;
using LayoutToolBar = LiteCad.UI.Layout.ToolBar;

namespace LiteCad.Tests;

public class RussianLocalizationTests : IDisposable
{
    private static readonly ResourceManager ResourceManager =
        new("LiteCad.Resources.Strings", typeof(Strings).Assembly);

    private readonly string _tempSettingsDirectory;
    private readonly string _tempRoot;

    public RussianLocalizationTests()
    {
        _tempSettingsDirectory = Path.Combine(Path.GetTempPath(), "LiteCad.Tests", Guid.NewGuid().ToString("N"));
        _tempRoot = Path.Combine(Path.GetTempPath(), "LiteCadRuTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempSettingsDirectory);
        Directory.CreateDirectory(_tempRoot);
        LanguageSettingsStore.SetSettingsDirectoryForTests(_tempSettingsDirectory);
        LocalizationManager.Instance.Initialize(AppLanguage.English);
    }

    public void Dispose()
    {
        LanguageSettingsStore.SetSettingsDirectoryForTests(null);
        LocalizationManager.Instance.Initialize(AppLanguage.English);
        if (Directory.Exists(_tempSettingsDirectory))
        {
            Directory.Delete(_tempSettingsDirectory, recursive: true);
        }

        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void StringsRuResx_Exists()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Resources", "Strings.ru.resx"));
        Assert.True(File.Exists(path), $"Missing Russian resource file: {path}");
    }

    [Fact]
    public void AllEnglishKeys_PresentInRussianResx()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var englishKeys = LoadResxKeys(Path.Combine(repoRoot, "Resources", "Strings.resx"));
        var russianKeys = LoadResxKeys(Path.Combine(repoRoot, "Resources", "Strings.ru.resx"));

        Assert.Equal(englishKeys.Count, russianKeys.Count);
        Assert.Empty(englishKeys.Except(russianKeys));
        Assert.Empty(russianKeys.Except(englishKeys));
    }

    [Fact]
    public void RussianLanguage_ShowsRussianText_ForCoreElements()
    {
        LocalizationManager.Instance.SetLanguage(AppLanguage.Russian, persist: false);

        Assert.Equal("Ось", Strings.Tool_Axis);
        Assert.Equal("Заливка", Strings.Tool_Fill);
        Assert.Equal("_Файл", Strings.Get("Menu.Header.File"));
        Assert.Equal("Готово", Strings.Status_Ready);
        Assert.Equal("LiteCad 0.4.1", Strings.Label_AppTitle);
        Assert.Equal("Проект", Strings.Label_DefaultProjectName);
        Assert.Equal(" м²", Strings.Format_AreaSuffix);
        Assert.Equal("Предпросмотр PDF", Strings.Get("Dialog.PdfPreview.Title"));
        Assert.Equal("Книжная", Strings.Get("PdfOrientation.Portrait"));
    }

    [Fact]
    public void EnglishLanguage_ShowsEnglishText_ForCoreElements()
    {
        LocalizationManager.Instance.SetLanguage(AppLanguage.Russian, persist: false);
        LocalizationManager.Instance.SetLanguage(AppLanguage.English, persist: false);

        Assert.Equal("Axis", Strings.Tool_Axis);
        Assert.Equal("Fill", Strings.Tool_Fill);
        Assert.Equal("_File", Strings.Get("Menu.Header.File"));
        Assert.Equal("Ready", Strings.Status_Ready);
        Assert.Equal("Project", Strings.Label_DefaultProjectName);
        Assert.Equal("PDF Preview", Strings.Get("Dialog.PdfPreview.Title"));
    }

    [Fact]
    public void Fallback_ReturnsEnglishThenKey_ForMissingRussianEntry()
    {
        LocalizationManager.Instance.SetLanguage(AppLanguage.Russian, persist: false);

        var english = ResourceManager.GetString("Status.Ready", AppLanguage.EnglishCulture);
        var missing = Strings.Get("Localization.Test.Missing.Key.12345");

        Assert.Equal("Ready", english);
        Assert.Equal("Localization.Test.Missing.Key.12345", missing);
    }

    [Fact]
    public void RussianLanguage_PersistsBetweenRestarts()
    {
        LocalizationManager.Instance.SetLanguage(AppLanguage.Russian);
        LocalizationManager.Instance.Initialize(LanguageSettingsStore.LoadLanguage());

        Assert.Equal(AppLanguage.Russian, LocalizationManager.Instance.Language);
    }

    [Fact]
    public void RussianLanguage_IsNotPartOfCadDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(1, 0));

        LocalizationManager.Instance.SetLanguage(AppLanguage.Russian);

        Assert.DoesNotContain(document.GetType().GetProperties(), property => property.Name == "Language");
    }

    [Fact]
    public void MenuBar_ShowsRussianHeaders_WhenRussianSelected()
    {
        WpfTestUtilities.RunSta(() =>
        {
            LocalizationManager.Instance.SetLanguage(AppLanguage.Russian, persist: false);
            var window = WpfTestUtilities.CreateHiddenWindow(new MenuBar());
            try
            {
                var menuBar = (MenuBar)window.Content!;
                var headers = menuBar.FindChildren<MenuItem>()
                    .Select(item => item.Header as string)
                    .Where(header => !string.IsNullOrWhiteSpace(header))
                    .ToList();

                Assert.Contains("_Файл", headers);
                Assert.Contains("_Правка", headers);
                Assert.Contains("_Инструменты", headers);
                Assert.Contains("_Тема", headers);
                Assert.Contains("_Справка", headers);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ToolBar_ShowsRussianTooltips_WhenRussianSelected()
    {
        WpfTestUtilities.RunSta(() =>
        {
            LocalizationManager.Instance.SetLanguage(AppLanguage.Russian, persist: false);
            var window = WpfTestUtilities.CreateHiddenWindow(new LayoutToolBar());
            try
            {
                var toolBar = (LayoutToolBar)window.Content!;
                toolBar.RefreshLocalizedLabels();

                var tooltips = toolBar.FindChildren<Button>()
                    .Select(button => button.ToolTip as string)
                    .Where(tooltip => !string.IsNullOrWhiteSpace(tooltip))
                    .ToList();

                Assert.Contains(tooltips, tooltip => tooltip!.Contains("Ось", StringComparison.Ordinal));
                Assert.Contains(tooltips, tooltip => tooltip!.Contains("Заливка грани", StringComparison.Ordinal));
                Assert.Contains(tooltips, tooltip => tooltip!.Contains("Выбор", StringComparison.Ordinal));
                Assert.All(tooltips, tooltip =>
                {
                    Assert.DoesNotContain("Copy", tooltip, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Polygon edit", tooltip, StringComparison.OrdinalIgnoreCase);
                });
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void PropertiesPanel_ShowsRussianLabels_WhenRussianSelected()
    {
        WpfTestUtilities.RunSta(() =>
        {
            LocalizationManager.Instance.SetLanguage(AppLanguage.Russian, persist: false);
            var window = WpfTestUtilities.CreateHiddenWindow(new PropertiesPanel());
            try
            {
                var panel = (PropertiesPanel)window.Content!;
                panel.SetActiveTool(ToolId.Axis);

                var labels = panel.FindChildren<TextBlock>()
                    .Select(block => block.Text)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToList();

                Assert.Contains("Свойства", labels);
                Assert.Contains("Инструмент", labels);
                Assert.Contains("Ось", labels);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void StatusBar_ShowsRussianReadyText_WhenRussianSelected()
    {
        WpfTestUtilities.RunSta(() =>
        {
            LocalizationManager.Instance.SetLanguage(AppLanguage.Russian, persist: false);
            var window = WpfTestUtilities.CreateHiddenWindow(new StatusBar());
            try
            {
                var statusBar = (StatusBar)window.Content!;
                var statusText = statusBar.FindChildren<TextBlock>()
                    .First(block => block.Name == "StatusText")
                    .Text;

                Assert.Equal("Готово", statusText);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void PdfPreview_ShowsRussianUi_WhenRussianSelected()
    {
        WpfTestUtilities.RunSta(() =>
        {
            LocalizationManager.Instance.SetLanguage(AppLanguage.Russian, persist: false);
            var document = new CadDocument();
            TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0));
            var preview = new PdfPreviewWindow(
                document,
                LinearDisplayUnit.Millimeters,
                new Renderer(),
                Path.Combine(_tempRoot, "preview.pdf"),
                null);
            try
            {
                preview.Show();
                preview.UpdateLayout();

                Assert.Equal("Предпросмотр PDF", preview.Title);
                var texts = preview.FindChildren<TextBlock>()
                    .Select(block => block.Text)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToList();

                Assert.Contains("Предпросмотр PDF", texts);
                Assert.Contains("Формат:", texts);
                Assert.Contains("Ориентация:", texts);
                Assert.Equal("Отмена", preview.FindChildren<Button>().First(button => button.Name == "CancelButton").Content);
                Assert.Equal("Сохранить PDF", preview.FindChildren<Button>().First(button => button.Name == "SaveButton").Content);
            }
            finally
            {
                preview.Close();
            }
        });
    }

    [Fact]
    public void Copy_RemainsInEditMenu_NotInToolBar()
    {
        WpfTestUtilities.RunSta(() =>
        {
            LocalizationManager.Instance.SetLanguage(AppLanguage.Russian, persist: false);
            var menuWindow = WpfTestUtilities.CreateHiddenWindow(new MenuBar());
            var toolWindow = WpfTestUtilities.CreateHiddenWindow(new LayoutToolBar());
            try
            {
                var toolTags = ((LayoutToolBar)toolWindow.Content!).FindChildren<Button>()
                    .Select(button => button.Tag as string)
                    .ToList();

                Assert.Equal("_Копировать", Strings.Get("Menu.Header.Copy"));
                Assert.Equal("Копировать", Strings.Menu_Copy);
                Assert.DoesNotContain(toolTags, tag => tag == "Copy");
                Assert.DoesNotContain(toolTags, tag => tag == "PolygonEdit");
            }
            finally
            {
                menuWindow.Close();
                toolWindow.Close();
            }
        });
    }

    [Fact]
    public void Theme_RemainsIndependentFromLanguage()
    {
        ThemeManager.Instance.Initialize(AppTheme.Dark);
        LocalizationManager.Instance.SetLanguage(AppLanguage.Russian, persist: false);

        Assert.Equal(AppTheme.Dark, ThemeManager.Instance.Theme);
        Assert.Equal(AppLanguage.Russian, LocalizationManager.Instance.Language);

        LocalizationManager.Instance.SetLanguage(AppLanguage.English, persist: false);
        Assert.Equal(AppTheme.Dark, ThemeManager.Instance.Theme);
    }

    private static HashSet<string> LoadResxKeys(string path)
        => XDocument.Load(path)
            .Descendants("data")
            .Select(node => (string?)node.Attribute("name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);
}

internal static class VisualTreeTestExtensions
{
    public static IEnumerable<T> FindChildren<T>(this DependencyObject parent)
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

            foreach (var nested in FindChildren<T>(child))
            {
                yield return nested;
            }
        }
    }
}
