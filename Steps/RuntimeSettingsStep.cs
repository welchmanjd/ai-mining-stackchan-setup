using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AiStackchanSetup.Services;

namespace AiStackchanSetup.Steps;

public sealed class RuntimeSettingsStep : StepBase
{
    public override int Index => 8;
    public override string Title => "追加設定";
    public override string Description => "追加設定を保存して再起動します。";
    public override string PrimaryActionText => "送信";

    public override async Task<StepResult> ExecuteAsync(StepContext context, CancellationToken token)
    {
        var vm = context.ViewModel;
        if (vm.SelectedPort == null)
        {
            return StepResult.Fail("COMポートが選択されていません", canRetry: false);
        }

        if (vm.WifiEnabled && vm.AiEnabled && string.IsNullOrWhiteSpace(vm.ConfigOpenAiKey) && !(vm.OpenAiKeyStored && vm.ReuseOpenAiKey))
        {
            return StepResult.Fail("OpenAI APIキーが未入力です", canRetry: false);
        }
        if (vm.WifiEnabled && (vm.MiningEnabled || vm.AiEnabled) && string.IsNullOrWhiteSpace(vm.AzureKey) && !(vm.AzureKeyStored && vm.ReuseAzureKey))
        {
            return StepResult.Fail("Azureキーが未入力です", canRetry: false);
        }

        vm.IsBusy = true;
        vm.StatusMessage = "最終確認中...";
        vm.ErrorMessage = "";

        try
        {
            var needOpenAi = vm.WifiEnabled && vm.AiEnabled;
            var needAzure = vm.WifiEnabled && (vm.MiningEnabled || vm.AiEnabled);

            var openAiExcluded = needOpenAi && vm.OpenAiKeyStored && vm.ReuseOpenAiKey;
            var azureExcluded = needAzure && vm.AzureKeyStored && vm.ReuseAzureKey;

            var openAiOk = !needOpenAi;
            var azureOk = !needAzure;

            if (!needOpenAi)
            {
                SetSummaryNeutral(vm, openAi: true, "対象外（AI機能OFF）");
            }
            else if (openAiExcluded)
            {
                SetSummaryNeutral(vm, openAi: true, "M5StackCore2内に保存されている情報を再利用するため検証対象外");
                openAiOk = true;
            }
            else
            {
                var openAiResult = await context.RetryPolicy.ExecuteWithTimeoutAsync(
                    ct => context.ApiTestService.TestAsync(vm.ConfigOpenAiKey, ct),
                    TimeSpan.FromSeconds(25),
                    maxAttempts: 3,
                    baseDelay: TimeSpan.FromMilliseconds(400),
                    backoffFactor: 2,
                    token);

                if (openAiResult.Success)
                {
                    SetSummaryOk(vm, openAi: true, "利用可能です");
                    openAiOk = true;
                }
                else
                {
                    SetSummaryNg(vm, openAi: true, $"利用できません: {openAiResult.Message}");
                }
            }

            if (!needAzure)
            {
                SetSummaryNeutral(vm, openAi: false, "対象外（Wi-Fi/機能OFF）");
            }
            else if (azureExcluded)
            {
                SetSummaryNeutral(vm, openAi: false, "M5StackCore2内に保存されている情報を再利用するため検証対象外");
                azureOk = true;
            }
            else
            {
                var azureResult = await context.RetryPolicy.ExecuteWithTimeoutAsync(
                    ct => context.ApiTestService.TestAzureSpeechAsync(vm.AzureKey, vm.AzureRegion, vm.AzureCustomSubdomain, ct),
                    TimeSpan.FromSeconds(25),
                    maxAttempts: 3,
                    baseDelay: TimeSpan.FromMilliseconds(400),
                    backoffFactor: 2,
                    token);

                if (azureResult.Success)
                {
                    SetSummaryOk(vm, openAi: false, "利用可能です");
                    azureOk = true;
                }
                else
                {
                    SetSummaryNg(vm, openAi: false, $"利用できません: {azureResult.Message}");
                }
            }

            if (!openAiOk || !azureOk)
            {
                vm.StatusMessage = "APIキー検証に失敗しました";
                return StepResult.Fail("APIキー検証に失敗しました。設定は適用していません。", canRetry: true);
            }

            vm.StatusMessage = "設定を送信中...";
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

            if (!string.IsNullOrWhiteSpace(setResult.Message) && !setResult.Message.Equals("OK", StringComparison.OrdinalIgnoreCase))
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
                vm.ErrorMessage = $"設定保存に失敗しました: {applyResult.Message}";
                vm.LastError = applyResult.Message;
                vm.StatusMessage = "設定保存に失敗しました";
                return StepResult.Fail($"設定保存に失敗しました: {applyResult.Message}");
            }

            if (vm.CaptureSerialLogAfterReboot)
            {
                vm.StatusMessage = "設定保存後の起動ログを取得中...(60秒)";
                try
                {
                    var rebootLog = await context.SerialService.CapturePostRebootLogAsync(
                        vm.SelectedPort.PortName,
                        TimeSpan.FromSeconds(60),
                        token);
                    if (!string.IsNullOrWhiteSpace(rebootLog))
                    {
                        var path = LogService.CreateDeviceLogPath();
                        await File.WriteAllTextAsync(path, rebootLog, token);
                        vm.DeviceLogPath = path;
                        vm.StatusMessage = $"設定を保存して再起動しました。ログ保存: {path}";
                    }
                    else
                    {
                        vm.StatusMessage = "設定を保存して再起動しました（60秒のログ出力なし）";
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    vm.StatusMessage = $"設定を保存して再起動しました（ログ取得失敗: {ex.Message}）";
                }
            }
            else
            {
                vm.StatusMessage = "設定を保存して再起動しました。";
            }
            return StepResult.Ok();
        }
        catch (OperationCanceledException)
        {
            vm.StatusMessage = "中止しました";
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
            vm.ErrorMessage = "設定適用に失敗しました";
            vm.LastError = ex.Message;
            vm.StatusMessage = "設定適用に失敗しました";
            return StepResult.Fail("設定適用に失敗しました");
        }
        finally
        {
            vm.IsBusy = false;
        }
    }

    private static void SetSummaryNeutral(ViewModels.MainViewModel vm, bool openAi, string message)
    {
        var brush = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
        var bg = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
        if (openAi)
        {
            vm.ApiTestSummary = message;
            vm.ApiTestSummaryBrush = brush;
            vm.ApiTestSummaryBackground = bg;
        }
        else
        {
            vm.AzureTestSummary = message;
            vm.AzureTestSummaryBrush = brush;
            vm.AzureTestSummaryBackground = bg;
        }
    }

    private static void SetSummaryOk(ViewModels.MainViewModel vm, bool openAi, string message)
    {
        var brush = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
        var bg = new SolidColorBrush(Color.FromRgb(0xDC, 0xF7, 0xE3));
        if (openAi)
        {
            vm.ApiTestSummary = message;
            vm.ApiTestSummaryBrush = brush;
            vm.ApiTestSummaryBackground = bg;
        }
        else
        {
            vm.AzureTestSummary = message;
            vm.AzureTestSummaryBrush = brush;
            vm.AzureTestSummaryBackground = bg;
        }
    }

    private static void SetSummaryNg(ViewModels.MainViewModel vm, bool openAi, string message)
    {
        var brush = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
        var bg = new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));
        if (openAi)
        {
            vm.ApiTestSummary = message;
            vm.ApiTestSummaryBrush = brush;
            vm.ApiTestSummaryBackground = bg;
        }
        else
        {
            vm.AzureTestSummary = message;
            vm.AzureTestSummaryBrush = brush;
            vm.AzureTestSummaryBackground = bg;
        }
    }
}
