using System.Windows;
using AiStackchanSetup.Services;

namespace AiStackchanSetup;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LogService.Initialize();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LogService.Shutdown();
        base.OnExit(e);
    }
}
