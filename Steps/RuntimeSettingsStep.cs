using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AiStackchanSetup.Services;

namespace AiStackchanSetup.Steps;

public sealed class RuntimeSettingsStep : StepBase
{
    public override int Index => 9;
    public override string Title => "設定保存";
    public override string Description => "APIキー確認と設定保存を実行します。";
    public override string PrimaryActionText => "設定保存";

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
        vm.StatusMessage = "事前確認中...";
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
                SetSummaryNeutral(vm, openAi: true, "対象外 (Wi-Fi/AI OFF)");
            }
            else if (openAiExcluded)
            {
                SetSummaryNeutral(vm, openAi: true, "M5StackCore2保存済みキーを再利用するため確認をスキップ");
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
                    SetSummaryOk(vm, openAi: true, "事前確認に成功");
                    openAiOk = true;
                }
                else
                {
                    SetSummaryNg(vm, openAi: true, $"事前確認に失敗: {openAiResult.Message}");
                }
            }

            if (!needAzure)
            {
                SetSummaryNeutral(vm, openAi: false, "対象外 (Wi-Fi/機能 OFF)");
            }
            else if (azureExcluded)
            {
                SetSummaryNeutral(vm, openAi: false, "M5StackCore2保存済みキーを再利用するため確認をスキップ");
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
                    SetSummaryOk(vm, openAi: false, "事前確認に成功");
                    azureOk = true;
                }
                else
                {
                    SetSummaryNg(vm, openAi: false, $"事前確認に失敗: {azureResult.Message}");
                }
            }

            if (!openAiOk || !azureOk)
            {
                vm.StatusMessage = "APIキー確認に失敗しました";
                return StepResult.Fail("APIキー確認に失敗しました。設定を見直して再試行してください。", canRetry: true);
            }

            vm.StatusMessage = "設定を送信中...";
            var config = vm.BuildDeviceConfig();
            Serilog.Log.Information(
                "Config send flags wifi_enabled={Wifi} mining_enabled={Mining} ai_enabled={Ai}",
                config.WifiEnabled,
                config.MiningEnabled,
                config.AiEnabled);
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

            // Read-back verify: ensure feature flags (especially mining OFF) are truly persisted.
            async Task<(bool ok, bool? miningEnabled, string reason)> VerifyFlagsAsync()
            {
                var cfg = await context.SerialService.GetConfigJsonAsync(vm.SelectedPort.PortName, token);
                if (!cfg.Success || string.IsNullOrWhiteSpace(cfg.Json))
                {
                    return (false, null, cfg.Message);
                }

                try
                {
                    using var doc = JsonDocument.Parse(cfg.Json);
                    var root = doc.RootElement;
                    bool? miningValue = null;
                    if (root.TryGetProperty("mining_enabled", out var m))
                    {
                        if (m.ValueKind == JsonValueKind.True || m.ValueKind == JsonValueKind.False)
                        {
                            miningValue = m.GetBoolean();
                        }
                        else if (m.ValueKind == JsonValueKind.Number && m.TryGetInt32(out var n))
                        {
                            miningValue = n != 0;
                        }
                        else if (m.ValueKind == JsonValueKind.String)
                        {
                            var s = m.GetString();
                            if (s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)) miningValue = true;
                            if (s == "0" || string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) miningValue = false;
                        }
                    }

                    var expectedMining = vm.WifiEnabled && vm.MiningEnabled;
                    if (miningValue.HasValue && miningValue.Value != expectedMining)
                    {
                        return (false, miningValue, $"mining_enabled mismatch (expected={expectedMining}, actual={miningValue.Value})");
                    }

                    return (true, miningValue, "OK");
                }
                catch (Exception ex)
                {
                    return (false, null, ex.Message);
                }
            }

            var verify = await VerifyFlagsAsync();
            if (!verify.ok)
            {
                // Retry one more time with the same config if persisted flags differ.
                vm.StatusMessage = "設定を再確認中...";
                var retrySet = await context.SerialService.SendConfigAsync(vm.SelectedPort.PortName, config, token);
                if (!retrySet.Success)
                {
                    return StepResult.Fail($"設定反映確認に失敗しました: {verify.reason}", canRetry: true);
                }

                var retryApply = await context.SerialService.ApplyConfigAsync(vm.SelectedPort.PortName, token);
                if (!retryApply.Success)
                {
                    return StepResult.Fail($"設定反映確認に失敗しました: {verify.reason}", canRetry: true);
                }

                var verify2 = await VerifyFlagsAsync();
                if (!verify2.ok)
                {
                    return StepResult.Fail($"設定反映確認に失敗しました: {verify2.reason}", canRetry: true);
                }
            }

            if (vm.CaptureSerialLogAfterReboot)
            {
                const int captureSeconds = 60;
                try
                {
                    var captureTask = context.SerialService.CapturePostRebootLogAsync(
                        vm.SelectedPort.PortName,
                        TimeSpan.FromSeconds(captureSeconds),
                        token);

                    for (var elapsed = 0; elapsed < captureSeconds; elapsed++)
                    {
                        vm.StatusMessage = $"60秒ログを取得します... ({elapsed + 1}秒)";
                        var completed = await Task.WhenAny(captureTask, Task.Delay(1000, token)) == captureTask;
                        if (completed)
                        {
                            break;
                        }
                    }

                    var rebootLog = await captureTask;
                    if (!string.IsNullOrWhiteSpace(rebootLog))
                    {
                        var path = LogService.CreateDeviceLogPath();
                        await File.WriteAllTextAsync(path, rebootLog, token);
                        vm.DeviceLogPath = path;
                        vm.StatusMessage = $"設定を保存して起動しました。ログ保存: {path}";
                    }
                    else
                    {
                        vm.StatusMessage = "設定を保存して起動しました。(60秒のログ出力なし)";
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    vm.StatusMessage = $"設定を保存して起動しました。(ログ取得失敗: {ex.Message})";
                }
            }
            else
            {
                vm.StatusMessage = "設定を保存して起動しました。";
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
