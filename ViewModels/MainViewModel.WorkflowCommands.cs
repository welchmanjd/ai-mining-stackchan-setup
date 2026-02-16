using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AiStackchanSetup.Steps;
using Serilog;

namespace AiStackchanSetup.ViewModels;

public partial class MainViewModel
{
    // Responsibility: step workflow/navigation commands and shutdown flow.

    private async Task PrimaryAsync()
    {
        ErrorMessage = "";
        _stepCts?.Dispose();
        _stepCts = new CancellationTokenSource();
        RaisePropertyChanged(nameof(CanCancel));

        StepResult result;
        try
        {
            result = await _stepController.ExecuteCurrentAsync(_stepCts.Token);
        }
        catch (OperationCanceledException)
        {
            Log.Information("workflow.step.cancelled_by_user");
            result = StepResult.Cancelled();
        }
        finally
        {
            _stepCts.Dispose();
            _stepCts = null;
            RaisePropertyChanged(nameof(CanCancel));
        }

        if (result.Status == StepStatus.Success || result.Status == StepStatus.Skipped)
        {
            _stepController.MoveNext();
            AutoSkipOptionalSteps();
            _stepController.SyncStepMetadata();
            return;
        }

        if (result.Status == StepStatus.Cancelled)
        {
            StatusMessage = UiText.Cancelled;
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            ErrorMessage = result.ErrorMessage;
        }

        if (result.CanRetry)
        {
            PrimaryButtonText = UiText.Retry;
        }
    }

    private void AutoSkipOptionalSteps()
    {
        while (Step < _stepController.LastStepIndex)
        {
            var definition = StepDefinitions.GetByIndex(Step);
            if (definition.IsAvailable(this))
            {
                break;
            }

            _stepController.MoveNext();
        }
    }

    private void CancelCurrent()
    {
        _stepCts?.Cancel();
    }

    public void PrepareForShutdown()
    {
        try
        {
            CancelCurrent();
            _serialService.Close();
            _flashService.KillActiveProcesses();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "workflow.prepare_for_shutdown.failed");
        }
    }

    private void RequestShutdown()
    {
        PrepareForShutdown();
        Application.Current.Shutdown();
    }

    private void GoBack()
    {
        _stepController.MovePrevious();
        _stepController.SyncStepMetadata();
        BackCommand.RaiseCanExecuteChanged();
        SkipCommand.RaiseCanExecuteChanged();
    }

    private void SkipStep()
    {
        _stepController.Skip();
        _stepController.SyncStepMetadata();
        BackCommand.RaiseCanExecuteChanged();
        SkipCommand.RaiseCanExecuteChanged();
    }

    private void UpdatePrimaryButtonTextForCurrentStep()
    {
        if (Step == 2)
        {
            PrimaryButtonText = FlashModeSkip ? UiText.FlashSkipWrite : UiText.FlashWrite;
        }
    }

}
