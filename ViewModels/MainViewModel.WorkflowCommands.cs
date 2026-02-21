using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AiStackchanSetup.Infrastructure;
using AiStackchanSetup.Steps;
using Serilog;

namespace AiStackchanSetup.ViewModels;

public partial class MainViewModel
{
    // Responsibility: step workflow/navigation commands and shutdown flow.

    private async Task PrimaryAsync()
    {
        if (!ConfirmWifiPasswordEdgeWhitespace())
        {
            return;
        }

        ErrorMessage = "";
        ClearStepFailureState(clearAssistMessage: true);
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
            ClearStepFailureState(clearAssistMessage: true);
            _stepController.MoveNext();
            AutoSkipOptionalSteps();
            _stepController.SyncStepMetadata();
            return;
        }

        if (result.Status == StepStatus.Cancelled)
        {
            ClearStepFailureState(clearAssistMessage: true);
            StatusMessage = UiText.Cancelled;
            return;
        }

        ApplyFailureResult(result);
    }

    private bool ConfirmWifiPasswordEdgeWhitespace()
    {
        if (Step != StepDefinitions.Wifi.Index || !WifiEnabled)
        {
            return true;
        }

        if (ReuseWifiPassword && WifiPasswordStored)
        {
            return true;
        }

        if (!InputSanitizer.HasEdgeWhitespace(ConfigWifiPassword))
        {
            return true;
        }

        var message = "パスワードの先頭または末尾にスペースがあります。\nこのまま続けますか？";
        var result = MessageBox.Show(
            message,
            "確認",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
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
            StopBusyAssistTimer();
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
        ClearStepFailureState(clearAssistMessage: true);
        _stepController.MovePrevious();
        _stepController.SyncStepMetadata();
        BackCommand.RaiseCanExecuteChanged();
        SkipCommand.RaiseCanExecuteChanged();
    }

    private void SkipStep()
    {
        ClearStepFailureState(clearAssistMessage: true);
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

    private void ApplyFailureResult(StepResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            ErrorMessage = result.ErrorMessage;
        }

        StepGuidanceMessage = string.IsNullOrWhiteSpace(result.Guidance)
            ? UiText.GuidanceFallback
            : result.Guidance;
        CanRetryCurrentStep = result.CanRetry;
        ShowFailureActions = true;
        StatusAssistMessage = UiText.FailureActionsPrompt;

        if (result.CanRetry)
        {
            PrimaryButtonText = UiText.Retry;
        }
    }

    private void ClearStepFailureState(bool clearAssistMessage)
    {
        StepGuidanceMessage = string.Empty;
        ShowFailureActions = false;
        CanRetryCurrentStep = false;
        if (clearAssistMessage && !IsBusy)
        {
            StatusAssistMessage = string.Empty;
        }
    }

    private void HandleBusyStateChanged()
    {
        if (IsBusy)
        {
            StatusAssistMessage = StepText.ProcessingAssist;
            StartBusyAssistTimer();
            return;
        }

        StopBusyAssistTimer();
        StatusAssistMessage = ShowFailureActions
            ? UiText.FailureActionsPrompt
            : string.Empty;
    }

    private void StartBusyAssistTimer()
    {
        StopBusyAssistTimer();
        _busyAssistCts = new CancellationTokenSource();
        _ = ShowBusyAssistAfterDelayAsync(_busyAssistCts.Token);
    }

    private async Task ShowBusyAssistAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(_timeouts.LongRunningNoticeDelay, token);
            if (!token.IsCancellationRequested && IsBusy)
            {
                StatusAssistMessage = StepText.ProcessingLongAssist;
            }
        }
        catch (OperationCanceledException)
        {
            // no-op
        }
    }

    private void StopBusyAssistTimer()
    {
        _busyAssistCts?.Cancel();
        _busyAssistCts?.Dispose();
        _busyAssistCts = null;
    }

}
