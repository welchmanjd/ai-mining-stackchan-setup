using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.Models;
using Serilog;

namespace AiStackchanSetup.Services;

public class ApiTestService
{
    public const string ModelsEndpoint = "https://api.openai.com/v1/models";
    public const string ResponsesEndpoint = "https://api.openai.com/v1/responses";
    public const string AzureSpeechVoicesPath = "/cognitiveservices/voices/list";
    public const string AzureSpeechTtsPath = "/cognitiveservices/v1";

    public async Task<ApiTestResult> TestAsync(string apiKey, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new ApiTestResult { Success = false, Message = "APIキーが未入力です" };
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var modelsResponse = await client.GetAsync(ModelsEndpoint, token);
            if (!modelsResponse.IsSuccessStatusCode)
            {
                return new ApiTestResult
                {
                    Success = false,
                    StatusCode = modelsResponse.StatusCode,
                    Message = $"HTTP {(int)modelsResponse.StatusCode}"
                };
            }

            var modelId = await SelectPreferredModelAsync(modelsResponse, token);
            if (string.IsNullOrWhiteSpace(modelId))
            {
                return new ApiTestResult { Success = false, Message = "利用可能なモデルが見つかりません" };
            }

            var payload = new
            {
                model = modelId,
                input = "ping",
                max_output_tokens = 1
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, ResponsesEndpoint);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

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

        var ttsEndpoint = ResolveAzureTtsEndpoint(region, customSubdomain);
        if (string.IsNullOrWhiteSpace(ttsEndpoint))
        {
            return new ApiTestResult { Success = false, Message = "Azure設定が不足しています" };
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);
            client.DefaultRequestHeaders.Add("User-Agent", "AiStackchanSetup");

            var (voiceName, voiceLocale) = await ResolveAzureVoiceAsync(client, region, customSubdomain, token);
            var ssml = BuildSsml(voiceLocale, voiceName);

            using var request = new HttpRequestMessage(HttpMethod.Post, ttsEndpoint);
            request.Headers.Add("X-Microsoft-OutputFormat", "riff-24khz-16bit-mono-pcm");
            request.Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");

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

    private static string ResolveAzureTtsEndpoint(string region, string customSubdomain)
    {
        if (!string.IsNullOrWhiteSpace(customSubdomain))
        {
            var trimmed = customSubdomain.Trim().TrimEnd('/');
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var normalized = trimmed.TrimEnd('/');
                if (normalized.EndsWith(AzureSpeechTtsPath, StringComparison.OrdinalIgnoreCase))
                {
                    return normalized;
                }
                if (normalized.EndsWith("/tts", StringComparison.OrdinalIgnoreCase))
                {
                    return normalized + AzureSpeechTtsPath;
                }
                return normalized + "/tts" + AzureSpeechTtsPath;
            }

            if (trimmed.Contains("cognitiveservices.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                return $"https://{trimmed}/tts{AzureSpeechTtsPath}";
            }

            return $"https://{trimmed}.cognitiveservices.azure.com/tts{AzureSpeechTtsPath}";
        }

        if (!string.IsNullOrWhiteSpace(region))
        {
            var trimmed = region.Trim();
            return $"https://{trimmed}.tts.speech.microsoft.com{AzureSpeechTtsPath}";
        }

        return string.Empty;
    }

    private static string ResolveAzureVoicesEndpoint(string region, string customSubdomain)
    {
        if (!string.IsNullOrWhiteSpace(customSubdomain))
        {
            var trimmed = customSubdomain.Trim().TrimEnd('/');
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var normalized = trimmed.TrimEnd('/');
                if (normalized.EndsWith(AzureSpeechVoicesPath, StringComparison.OrdinalIgnoreCase))
                {
                    return normalized;
                }
                if (normalized.EndsWith("/tts", StringComparison.OrdinalIgnoreCase))
                {
                    return normalized + AzureSpeechVoicesPath;
                }
                return normalized + "/tts" + AzureSpeechVoicesPath;
            }

            if (trimmed.Contains("cognitiveservices.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                return $"https://{trimmed}/tts{AzureSpeechVoicesPath}";
            }

            return $"https://{trimmed}.cognitiveservices.azure.com/tts{AzureSpeechVoicesPath}";
        }

        if (!string.IsNullOrWhiteSpace(region))
        {
            var trimmed = region.Trim();
            return $"https://{trimmed}.tts.speech.microsoft.com{AzureSpeechVoicesPath}";
        }

        return string.Empty;
    }

    private static async Task<(string Name, string Locale)> ResolveAzureVoiceAsync(HttpClient client, string region, string customSubdomain, CancellationToken token)
    {
        var endpoint = ResolveAzureVoicesEndpoint(region, customSubdomain);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return ("en-US-JennyNeural", "en-US");
        }

        try
        {
            var response = await client.GetAsync(endpoint, token);
            if (!response.IsSuccessStatusCode)
            {
                return ("en-US-JennyNeural", "en-US");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ("en-US-JennyNeural", "en-US");
            }

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("ShortName", out var shortNameProp))
                {
                    continue;
                }

                var shortName = shortNameProp.GetString();
                if (string.IsNullOrWhiteSpace(shortName))
                {
                    continue;
                }

                var locale = item.TryGetProperty("Locale", out var localeProp)
                    ? localeProp.GetString() ?? "en-US"
                    : "en-US";
                return (shortName, locale);
            }
        }
        catch
        {
            // ignore fallback
        }

        return ("en-US-JennyNeural", "en-US");
    }

    private static string BuildSsml(string locale, string voiceName)
    {
        var lang = string.IsNullOrWhiteSpace(locale) ? "en-US" : locale.Trim();
        var voice = string.IsNullOrWhiteSpace(voiceName) ? "en-US-JennyNeural" : voiceName.Trim();
        return $"<speak version='1.0' xml:lang='{lang}'><voice name='{voice}'>ping</voice></speak>";
    }

    private static async Task<string?> SelectPreferredModelAsync(HttpResponseMessage modelsResponse, CancellationToken token)
    {
        await using var stream = await modelsResponse.Content.ReadAsStreamAsync(token);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var modelIds = new List<string>();
        foreach (var item in data.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idProp))
            {
                continue;
            }

            var id = idProp.GetString();
            if (!string.IsNullOrWhiteSpace(id))
            {
                modelIds.Add(id);
            }
        }

        if (modelIds.Count == 0)
        {
            return null;
        }

        var preferred = new[]
        {
            "gpt-4o-mini",
            "gpt-4o",
            "o4-mini",
            "o3-mini"
        };

        foreach (var name in preferred)
        {
            var match = modelIds.FirstOrDefault(id => string.Equals(id, name, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match))
            {
                return match;
            }
        }

        return modelIds.FirstOrDefault(id => id.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase) ||
                                            id.StartsWith("o", StringComparison.OrdinalIgnoreCase));
    }
}
