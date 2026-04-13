using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MindMapApp.Services;
using MindMapApp.ViewModels;
using MindMapApp.Views;

namespace MindMapApp;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var persistenceService = new MindMapPersistenceService();
            var mainWindowViewModel = new MainWindowViewModel(persistenceService);

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
