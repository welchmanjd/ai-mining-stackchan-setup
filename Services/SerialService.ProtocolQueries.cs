using System;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Models;

namespace AiStackchanSetup.Services;

public partial class SerialService
{
    public Task<HelloResult> HelloAsync(string portName)
    {
        return HelloAsync(portName, CancellationToken.None);
    }

    public async Task<HelloResult> HelloAsync(string portName, CancellationToken token)
    {
        try
        {
            var response = await SendCommandAsync(portName, "HELLO", TimeSpan.FromSeconds(8), token);
            if (response.StartsWith("@OK HELLO", StringComparison.OrdinalIgnoreCase))
            {
                return new HelloResult { Success = true, Message = "OK" };
            }

            return new HelloResult { Success = false, Message = "応答が想定外です" };
        }
        catch (SerialCommandException ex)
        {
            return new HelloResult { Success = false, Message = ex.Reason };
        }
        catch (TimeoutException ex)
        {
            return new HelloResult { Success = false, Message = ex.Message };
        }
    }

    public Task<CommandResult> PingAsync(string portName)
    {
        return PingAsync(portName, CancellationToken.None);
    }

    public async Task<CommandResult> PingAsync(string portName, CancellationToken token)
    {
        try
        {
            var response = await SendCommandAsync(portName, "PING", TimeSpan.FromSeconds(8), token);
            if (response.StartsWith("@OK PONG", StringComparison.OrdinalIgnoreCase))
            {
                return new CommandResult { Success = true, Message = "OK" };
            }

            return new CommandResult { Success = false, Message = "応答が想定外です" };
        }
        catch (SerialCommandException ex)
        {
            return new CommandResult { Success = false, Message = ex.Reason };
        }
        catch (TimeoutException ex)
        {
            return new CommandResult { Success = false, Message = ex.Message };
        }
    }

    public Task<DeviceInfoResult> GetInfoAsync(string portName)
    {
        return GetInfoAsync(portName, CancellationToken.None);
    }

    public Task<DeviceInfoResult> GetInfoAsync(string portName, CancellationToken token)
    {
        return GetInfoAsync(portName, TimeSpan.FromSeconds(8), token);
    }

    public async Task<DeviceInfoResult> GetInfoAsync(string portName, TimeSpan timeout, CancellationToken token)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var response = await SendCommandAsync(portName, "GET INFO", timeout, token);
                if (!response.StartsWith("@INFO", StringComparison.OrdinalIgnoreCase))
                {
                    return new DeviceInfoResult { Success = false, Message = "応答が想定外です" };
                }

                var json = response["@INFO".Length..].Trim();
                var info = DeviceInfo.TryParse(json);
                if (info == null)
                {
                    return new DeviceInfoResult { Success = false, Message = "INFO JSONを解析できません" };
                }

                return new DeviceInfoResult { Success = true, Message = "OK", RawJson = json, Info = info };
            }
            catch (SerialCommandException ex) when (attempt == 0 && SerialProtocolLogic.IsTransientInfoSyncNoise(ex.Reason))
            {
                await Task.Delay(180, token);
                continue;
            }
            catch (SerialCommandException ex)
            {
                return new DeviceInfoResult { Success = false, Message = ex.Reason };
            }
            catch (TimeoutException ex)
            {
                return new DeviceInfoResult { Success = false, Message = ex.Message };
            }
        }

        return new DeviceInfoResult { Success = false, Message = "GET INFO failed after retry" };
    }

    public Task<ConfigJsonResult> GetConfigJsonAsync(string portName)
    {
        return GetConfigJsonAsync(portName, CancellationToken.None);
    }

    public async Task<ConfigJsonResult> GetConfigJsonAsync(string portName, CancellationToken token)
    {
        try
        {
            var response = await SendCommandAsync(portName, "GET CFG", TimeSpan.FromSeconds(8), token);
            if (!response.StartsWith("@CFG", StringComparison.OrdinalIgnoreCase))
            {
                return new ConfigJsonResult { Success = false, Message = "応答が想定外です" };
            }

            var json = response["@CFG".Length..].Trim();
            return new ConfigJsonResult { Success = true, Message = "OK", Json = json };
        }
        catch (SerialCommandException ex)
        {
            return new ConfigJsonResult { Success = false, Message = ex.Reason };
        }
        catch (TimeoutException ex)
        {
            return new ConfigJsonResult { Success = false, Message = ex.Message };
        }
    }
}
