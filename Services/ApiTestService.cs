using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Models;
using Serilog;

namespace AiStackchanSetup.Services;

public class ApiTestService
{
    public const string ModelsEndpoint = "https://api.openai.com/v1/models";
    public const string AzureSpeechTokenPath = "/sts/v1.0/issueToken";

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

    public async Task<ApiTestResult> TestAzureSpeechAsync(string apiKey, string region, string customSubdomain, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new ApiTestResult { Success = false, Message = "未入力" };
        }

        var endpoint = ResolveAzureSpeechEndpoint(region, customSubdomain);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new ApiTestResult { Success = false, Message = "Azure設定が不足しています" };
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = new StringContent(string.Empty, Encoding.UTF8, "application/x-www-form-urlencoded");

            var response = await client.SendAsync(request, token);
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
            Log.Error(ex, "Azure Speech API test failed");
            return new ApiTestResult { Success = false, Message = ex.Message };
        }
    }

    private static string ResolveAzureSpeechEndpoint(string region, string customSubdomain)
    {
        if (!string.IsNullOrWhiteSpace(customSubdomain))
        {
            var trimmed = customSubdomain.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var normalized = trimmed.TrimEnd('/');
                if (normalized.EndsWith(AzureSpeechTokenPath, StringComparison.OrdinalIgnoreCase))
                {
                    return normalized;
                }
                return normalized + AzureSpeechTokenPath;
            }

            return $"https://{trimmed}.cognitiveservices.azure.com{AzureSpeechTokenPath}";
        }

        if (!string.IsNullOrWhiteSpace(region))
        {
            var trimmed = region.Trim();
            return $"https://{trimmed}.api.cognitive.microsoft.com{AzureSpeechTokenPath}";
        }

        return string.Empty;
    }
}
