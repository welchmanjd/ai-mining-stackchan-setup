using AiStackchanSetup.Infrastructure;
using AiStackchanSetup.Services;
using AiStackchanSetup.ViewModels;

namespace AiStackchanSetup.Steps;

public sealed class StepContext
{
    public StepContext(MainViewModel viewModel,
        ISerialService serialService,
        IFlashService flashService,
        IApiTestService apiTestService,
        ISupportPackService supportPackService,
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
    public ISerialService SerialService { get; }
    public IFlashService FlashService { get; }
    public IApiTestService ApiTestService { get; }
    public ISupportPackService SupportPackService { get; }
    public RetryPolicy RetryPolicy { get; }
    public StepTimeouts Timeouts { get; }
}
