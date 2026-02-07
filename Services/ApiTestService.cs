using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Models;
using Serilog;

namespace AiStackchanSetup.Services;

public class ApiTestService
{
    public const string ModelsEndpoint = "https://api.openai.com/v1/models";

    public async Task<ApiTestResult> TestAsync(string apiKey, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new ApiTestResult { Success = false, Message = "APIキーが未入力です" };
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await client.GetAsync(ModelsEndpoint, token);
            var success = response.IsSuccessStatusCode;

            return new ApiTestResult
            {
                Success = success,
                StatusCode = response.StatusCode,
                Message = success ? "OK" : $"HTTP {(int)response.StatusCode}"
            };
        }
        catch (TaskCanceledException)
        {
            return new ApiTestResult { Success = false, Message = "タイムアウト" };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "API test failed");
            return new ApiTestResult { Success = false, Message = ex.Message };
        }
    }
}
