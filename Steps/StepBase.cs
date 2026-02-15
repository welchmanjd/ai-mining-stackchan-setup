using System.Threading;
using System.Threading.Tasks;
using System;
using AiStackchanSetup.ViewModels;

namespace AiStackchanSetup.Steps;

public abstract class StepBase : IStep
{
    private readonly StepDefinition _definition;

    protected StepBase(StepDefinition definition, bool canRetry = true, bool canSkip = false)
    {
        _definition = definition;
        CanRetry = canRetry;
        CanSkip = canSkip;
    }

    public int Index => _definition.Index;
    public string Title => _definition.Title;
    public string Description => _definition.Description;
    public string PrimaryActionText => _definition.PrimaryActionText;
    public bool CanRetry { get; }
    public bool CanSkip { get; }

    public virtual void OnEnter(StepContext context)
    {
    }

    public virtual void OnLeave(StepContext context)
    {
    }

    protected async Task<StepResult> ExecuteBusyStepAsync(
        StepContext context,
        CancellationToken token,
        Func<Task<StepResult>> body,
        Action<MainViewModel>? before = null,
        Action<MainViewModel>? after = null,
        Func<MainViewModel, OperationCanceledException, StepResult>? onCancelled = null,
        Func<MainViewModel, TimeoutException, StepResult>? onTimeout = null,
        Func<MainViewModel, Exception, StepResult>? onError = null)
    {
        return await ExecuteStepCoreAsync(
            context,
            token,
            body,
            manageBusy: true,
            before,
            after,
            onCancelled,
            onTimeout,
            onError);
    }

    protected async Task<StepResult> ExecuteStepAsync(
        StepContext context,
        CancellationToken token,
        Func<Task<StepResult>> body,
        Action<MainViewModel>? before = null,
        Action<MainViewModel>? after = null,
        Func<MainViewModel, OperationCanceledException, StepResult>? onCancelled = null,
        Func<MainViewModel, TimeoutException, StepResult>? onTimeout = null,
        Func<MainViewModel, Exception, StepResult>? onError = null)
    {
        return await ExecuteStepCoreAsync(
            context,
            token,
            body,
            manageBusy: false,
            before,
            after,
            onCancelled,
            onTimeout,
            onError);
    }

    private async Task<StepResult> ExecuteStepCoreAsync(
        StepContext context,
        CancellationToken token,
        Func<Task<StepResult>> body,
        bool manageBusy,
        Action<MainViewModel>? before,
        Action<MainViewModel>? after,
        Func<MainViewModel, OperationCanceledException, StepResult>? onCancelled,
        Func<MainViewModel, TimeoutException, StepResult>? onTimeout,
        Func<MainViewModel, Exception, StepResult>? onError)
    {
        var vm = context.ViewModel;
        if (manageBusy)
        {
            vm.IsBusy = true;
        }
        before?.Invoke(vm);

        try
        {
            return await body();
        }
        catch (OperationCanceledException ex) when (token.IsCancellationRequested)
        {
            if (onCancelled != null)
            {
                return onCancelled(vm, ex);
            }

            vm.StatusMessage = StepText.Cancelled;
            return StepResult.Cancelled();
        }
        catch (TimeoutException ex)
        {
            if (onTimeout != null)
            {
                return onTimeout(vm, ex);
            }

            vm.StatusMessage = StepText.Timeout;
            vm.ErrorMessage = ex.Message;
            vm.LastError = ex.Message;
            return StepResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            if (onError != null)
            {
                return onError(vm, ex);
            }

            vm.ErrorMessage = ex.Message;
            vm.LastError = ex.Message;
            return StepResult.Fail(ex.Message);
        }
        finally
        {
            after?.Invoke(vm);
            if (manageBusy)
            {
                vm.IsBusy = false;
            }
        }
    }

    public abstract Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token);
}

