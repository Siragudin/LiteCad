using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LiteCad.Tests;

internal static class WpfTestUtilities
{
    private static Thread? _staThread;
    private static Dispatcher? _dispatcher;
    private static readonly ManualResetEventSlim StaThreadReady = new(false);
    private static readonly object StaThreadLock = new();

    public static void RunSta(Action action)
    {
        EnsureStaThread();
        Exception? captured = null;
        _dispatcher!.Invoke(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        if (captured is not null)
        {
            ExceptionDispatchInfo.Capture(captured).Throw();
        }
    }

    public static Window CreateHiddenWindow(UIElement? content = null)
    {
        EnsureStaThread();
        Window? window = null;
        _dispatcher!.Invoke(() =>
        {
            EnsureApplication();
            window = new Window
            {
                Content = content,
                Width = 800,
                Height = 600,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Visibility = Visibility.Hidden
            };
            window.Show();
            window.UpdateLayout();
        });

        return window!;
    }

    public static PresentationSource RequirePresentationSource(Visual visual)
    {
        PresentationSource? source = null;
        visual.Dispatcher.Invoke(() => source = PresentationSource.FromVisual(visual));
        if (source is null)
        {
            throw new InvalidOperationException("PresentationSource is unavailable for the visual tree.");
        }

        return source;
    }

    public static KeyEventArgs CreateKeyDown(Visual visual, Key key)
        => new(Keyboard.PrimaryDevice, RequirePresentationSource(visual), 0, key)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        };

    private static void EnsureStaThread()
    {
        if (_dispatcher is not null)
        {
            return;
        }

        lock (StaThreadLock)
        {
            if (_dispatcher is not null)
            {
                return;
            }

            _staThread = new Thread(() =>
            {
                EnsureApplication();
                _dispatcher = Dispatcher.CurrentDispatcher;
                StaThreadReady.Set();
                Dispatcher.Run();
            })
            {
                IsBackground = true
            };
            _staThread.SetApartmentState(ApartmentState.STA);
            _staThread.Start();
            StaThreadReady.Wait();
        }
    }

    private static void EnsureApplication()
    {
        if (Application.Current is null)
        {
            new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
        }
        else
        {
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        EnsureApplicationResources();
    }

    private static void EnsureApplicationResources()
    {
        if (Application.Current is null)
        {
            return;
        }

        if (Application.Current.Resources.MergedDictionaries.Any(dictionary =>
                dictionary.Source?.OriginalString.Contains("CadTheme.xaml", StringComparison.OrdinalIgnoreCase) == true))
        {
            return;
        }

        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/LiteCad;component/UI/Resources/CadTheme.xaml", UriKind.Relative)
        });
        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/LiteCad;component/UI/Resources/ToolIcons.xaml", UriKind.Relative)
        });
    }
}
