using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiStackchanSetup.Steps;

public sealed class RuntimeSettingsStep : StepBase
{
    public override int Index => 8;
    public override string Title => "追加設定";
    public override string Description => "追加した設定項目を送信して保存します。";
    public override string PrimaryActionText => "送信";

    public override async Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        var vm = context.ViewModel;
        if (vm.SelectedPort == null)
        {
            return StepResult.Fail("COMポートが選択されていません", canRetry: false);
        }

        if (vm.WifiEnabled && vm.AiEnabled && string.IsNullOrWhiteSpace(vm.ConfigOpenAiKey) && !vm.OpenAiKeyStored)
        {
            return StepResult.Fail("OpenAI APIキーが未入力です", canRetry: false);
        }
        if (vm.WifiEnabled && (vm.MiningEnabled || vm.AiEnabled) && string.IsNullOrWhiteSpace(vm.AzureKey) && !vm.AzureKeyStored)
        {
            return StepResult.Fail("Azure Speechキーが未入力です", canRetry: false);
        }

        vm.IsBusy = true;
        vm.StatusMessage = "設定を送信中...";

        try
        {
            var config = vm.BuildDeviceConfig();
            var setResult = await context.RetryPolicy.ExecuteWithTimeoutAsync(
                ct => context.SerialService.SendConfigAsync(vm.SelectedPort.PortName, config, ct),
                context.Timeouts.SendConfig,
                maxAttempts: 3,
                baseDelay: TimeSpan.FromMilliseconds(400),
                backoffFactor: 2,
                token);

            if (!setResult.Success)
            {
                vm.ErrorMessage = $"設定送信に失敗しました: {setResult.Message}";
                vm.LastError = setResult.Message;
                vm.StatusMessage = "設定送信に失敗しました";
                return StepResult.Fail($"設定送信に失敗しました: {setResult.Message}");
            }

            if (!string.IsNullOrWhiteSpace(setResult.Message) &&
                !setResult.Message.Equals("OK", StringComparison.OrdinalIgnoreCase))
            {
                vm.StatusMessage = setResult.Message;
            }

            var applyResult = await context.RetryPolicy.ExecuteWithTimeoutAsync(
                ct => context.SerialService.ApplyConfigAsync(vm.SelectedPort.PortName, ct),
                context.Timeouts.ApplyConfig,
                maxAttempts: 2,
                baseDelay: TimeSpan.FromMilliseconds(600),
                backoffFactor: 2,
                token);

            if (!applyResult.Success)
            {
                vm.ErrorMessage = $"設定反映に失敗しました: {applyResult.Message}";
                vm.LastError = applyResult.Message;
                vm.StatusMessage = "設定反映に失敗しました";
                return StepResult.Fail($"設定反映に失敗しました: {applyResult.Message}");
            }

            vm.StatusMessage = "設定を保存して再起動しました。";
            return StepResult.Ok();
        }
        catch (OperationCanceledException)
        {
            vm.StatusMessage = "中断しました";
            return StepResult.Cancelled();
        }
        catch (TimeoutException ex)
        {
            vm.ErrorMessage = ex.Message;
            vm.LastError = ex.Message;
            vm.StatusMessage = "タイムアウトしました";
            return StepResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            vm.ErrorMessage = "設定送信に失敗しました";
            vm.LastError = ex.Message;
            vm.StatusMessage = "設定送信に失敗しました";
            return StepResult.Fail("設定送信に失敗しました");
        }
        finally
        {
            vm.IsBusy = false;
        }
    }
}
