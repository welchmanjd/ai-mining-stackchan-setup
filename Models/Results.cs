using System.Net;

namespace AiStackchanSetup.Models;

public class FlashResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string LogPath { get; set; } = string.Empty;
}

public class ApiTestResult
{
    public bool Success { get; set; }
    public HttpStatusCode? StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class DeviceTestResult
{
    public bool Success { get; set; }
    public bool Skipped { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ConfigResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class HelloResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
}
