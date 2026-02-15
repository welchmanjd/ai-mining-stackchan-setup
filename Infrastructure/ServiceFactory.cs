using AiStackchanSetup.Services;
using AiStackchanSetup.Steps;
using AiStackchanSetup.ViewModels;

namespace AiStackchanSetup.Infrastructure;

public static class ServiceFactory
{
    public static MainViewModel CreateMainViewModel()
    {
        return new MainViewModel(
            new SerialService(),
            new FlashService(),
            new ApiTestService(),
            new SupportPackService(),
            new RetryPolicy(),
            new StepTimeouts());
    }
}
