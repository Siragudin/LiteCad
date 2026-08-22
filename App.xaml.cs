using System.Configuration;
using System.Data;
using System.Windows;
using LiteCad.Infrastructure;
using LiteCad.Resources;
using LiteCad.Services;

namespace LiteCad
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var settings = AppSettingsStore.Load();
            LocalizationManager.Instance.Initialize(settings.Language);
            ThemeManager.Instance.Initialize(settings.Theme);
            new ProjectStorage().EnsureProjectsDirectoryExists();
            base.OnStartup(e);
        }
    }

}
