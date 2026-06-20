using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using MiniCAD.App.Configuration;
using MiniCAD.App.ViewModels;
using MiniCAD.App.Views;

namespace MiniCAD.App;

public partial class App : Application
{
    private static readonly TimeSpan SplashDuration = TimeSpan.FromMilliseconds(1400);

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Apply the persisted colour theme before any window is shown.
        RequestedThemeVariant =
            string.Equals(AppConfig.Instance.Theme, "Dark", StringComparison.OrdinalIgnoreCase)
                ? ThemeVariant.Dark
                : ThemeVariant.Light;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;

            var splash = new SplashWindow();
            splash.Show();

            // Keep the splash up briefly, then open the real startup window and close it.
            Dispatcher.UIThread.Post(async () =>
            {
                await Task.Delay(SplashDuration);
                ShowStartupWindow(desktop);
                splash.Close();
            });
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ShowStartupWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        Window window = CreateStartupWindow();
        desktop.MainWindow = window;
        window.Show();
    }

    /// <summary>
    /// Default: the project overview window. If the user opted in (and a last project still
    /// exists), open straight into that project instead.
    /// </summary>
    private static Window CreateStartupWindow()
    {
        AppConfig config = AppConfig.Instance;

        if (config.OpenLastProjectOnStartup && config.LastProject is { } last && File.Exists(last))
        {
            var viewModel = new MainWindowViewModel();
            var main = new MainWindow { DataContext = viewModel };
            try
            {
                viewModel.OpenFromPath(last);
                return main;
            }
            catch
            {
                // Could not load the last project; fall through to the start window.
            }
        }

        return new StartWindow { DataContext = new StartWindowViewModel() };
    }
}
