using AiStackchanSetup.Infrastructure;
using AiStackchanSetup.Services;
using AiStackchanSetup.ViewModels;

namespace AiStackchanSetup.Steps;

public sealed class StepContext
{
    public StepContext(MainViewModel viewModel,
        SerialService serialService,
        FlashService flashService,
        ApiTestService apiTestService,
        SupportPackService supportPackService,
        RetryPolicy retryPolicy,
        StepTimeouts timeouts)
    {
        ViewModel = viewModel;
        SerialService = serialService;
        FlashService = flashService;
        ApiTestService = apiTestService;
        SupportPackService = supportPackService;
        RetryPolicy = retryPolicy;
        Timeouts = timeouts;
    }

    public MainViewModel ViewModel { get; }
    public SerialService SerialService { get; }
    public FlashService FlashService { get; }
    public ApiTestService ApiTestService { get; }
    public SupportPackService SupportPackService { get; }
    public RetryPolicy RetryPolicy { get; }
    public StepTimeouts Timeouts { get; }
}
