using System.Windows;
using AiStackchanSetup.Infrastructure;
using AiStackchanSetup.Services;

namespace AiStackchanSetup;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LogService.Initialize();

        var viewModel = ServiceFactory.CreateMainViewModel();
        var mainWindow = new MainWindow(viewModel);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LogService.Shutdown();
        base.OnExit(e);
    }
}
