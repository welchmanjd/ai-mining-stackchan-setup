using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AiStackchanSetup.Infrastructure;
using AiStackchanSetup.Models;
using AiStackchanSetup.Services;
using Microsoft.Win32;
using Serilog;

namespace AiStackchanSetup.ViewModels;

public partial class MainViewModel
{
    // Responsibility: firmware picker, logs, and support-pack generation commands.

    private async Task DumpDeviceLogAsync()
    {
        if (SelectedPort == null)
        {
            ErrorMessage = StepText.ComPortNotSelected;
            return;
        }

        ErrorMessage = "";
        IsBusy = true;
        StatusMessage = UiText.DeviceLogFetching;

        try
        {
            var deviceLog = await _serialService.DumpLogAsync(SelectedPort.PortName);
            if (string.IsNullOrWhiteSpace(deviceLog))
            {
                ErrorMessage = UiText.DeviceLogEmpty;
                return;
            }

            var config = BuildDeviceConfig();
            var sanitized = SensitiveDataRedactor.Redact(deviceLog, config);
            var path = LogService.CreateDeviceLogPath();
            await File.WriteAllTextAsync(path, sanitized);
            DeviceLogPath = path;
            StatusMessage = UiText.DeviceLogSaved(path);
        }
        catch (SerialCommandException ex)
        {
            Log.Warning(ex, "support.device_log_dump.unsupported");
            ErrorMessage = UiText.DeviceLogUnsupported;
            _lastError = ex.Message;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "support.device_log_dump.failed");
            ErrorMessage = UiText.DeviceLogFetchFailed;
            _lastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BrowseFirmware()
    {
        var dialog = new OpenFileDialog
        {
            Filter = UiText.BinFileDialogFilter
        };

        if (dialog.ShowDialog() == true)
        {
            FirmwarePath = dialog.FileName;
        }
    }

    private void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(LogService.LogDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = LogService.LogDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "support.open_log_folder.failed");
        }
    }

    private void OpenFlashLog()
    {
        try
        {
            Directory.CreateDirectory(LogService.LogDirectory);
            if (File.Exists(LogService.FlashLogPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = LogService.FlashLogPath,
                    UseShellExecute = true
                });
            }
            else
            {
                OpenLogFolder();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "support.open_flash_log.failed");
        }
    }

    private async Task CreateSupportPackAsync()
    {
        try
        {
            var config = BuildDeviceConfig();

            var summary = new SupportSummary
            {
                AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                DotNetVersion = Environment.Version.ToString(),
                OsVersion = Environment.OSVersion.ToString(),
                AppBaseDirectory = AppContext.BaseDirectory,
                FirmwarePath = FirmwarePath,
                FirmwareInfo = FirmwareInfoText,
                DetectedPorts = string.Join(",", Ports.Select(p => p.PortName)),
                SelectedPort = SelectedPort?.PortName ?? "",
                FlashResult = _lastFlashResult,
                ApiTest = _lastApiResult,
                DeviceTest = _lastDeviceResult,
                LastError = _lastError,
                DeviceInfoJson = string.IsNullOrWhiteSpace(DeviceInfoJson) ? _serialService.LastInfoJson : DeviceInfoJson,
                LastProtocolResponse = string.IsNullOrWhiteSpace(LastProtocolResponse) ? _serialService.LastProtocolResponse : LastProtocolResponse,
                Config = config.ToMasked()
            };

            string deviceLog;
            try
            {
                deviceLog = SelectedPort != null
                    ? await _serialService.DumpLogAsync(SelectedPort.PortName)
                    : string.Empty;
            }
            catch (SerialCommandException ex)
            {
                Log.Warning(ex, "support.device_log_dump.unsupported");
                deviceLog = string.Empty;
            }
            if (!string.IsNullOrWhiteSpace(deviceLog))
            {
                var sanitized = SensitiveDataRedactor.Redact(deviceLog, config);
                var path = LogService.CreateDeviceLogPath();
                await File.WriteAllTextAsync(path, sanitized);
                DeviceLogPath = path;
            }

            var zipPath = await _supportPackService.CreateSupportPackAsync(summary, config);
            StatusMessage = UiText.SupportPackCreated(zipPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "support.support_pack.failed");
            ErrorMessage = UiText.SupportPackCreationFailed;
        }
    }

}
