using System.Net;

namespace AiStackchanSetup.Models;

public class OperationResult
{
    public bool Success { get; set; }
    public bool Skipped { get; set; }
    public string Message { get; set; } = string.Empty;

    public bool IsFailure => !Success && !Skipped;
    public bool IsSuccessOrSkipped => Success || Skipped;

    public bool HasMessage(string expected, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        return string.Equals(Message, expected, comparison);
    }

    public static OperationResult Ok(string message = "OK")
    {
        return new OperationResult { Success = true, Message = message };
    }

    public static OperationResult Fail(string message)
    {
        return new OperationResult { Success = false, Message = message };
    }

    public static OperationResult Skip(string message = "SKIP")
    {
        return new OperationResult { Success = false, Skipped = true, Message = message };
    }
}

public class FlashResult : OperationResult
{
    public int ExitCode { get; set; }
    public string LogPath { get; set; } = string.Empty;
}

public class ApiTestResult : OperationResult
{
    public HttpStatusCode? StatusCode { get; set; }
}

public class DeviceTestResult : OperationResult
{
}

public class ConfigResult : OperationResult
{
}

public class CommandResult : OperationResult
{
}

public class DeviceInfoResult : OperationResult
{
    public string RawJson { get; set; } = string.Empty;
    public DeviceInfo? Info { get; set; }
}

public class ConfigJsonResult : OperationResult
{
    public string Json { get; set; } = string.Empty;
}

public class HelloResult : OperationResult
{
    public string RawJson { get; set; } = string.Empty;
    public DeviceHelloInfo? Info { get; set; }
}
