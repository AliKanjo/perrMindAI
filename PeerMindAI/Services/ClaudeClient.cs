using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PeerMindAI.Services;

/// <summary>The AI service failed; the message is safe to show to the user.</summary>
public class AiServiceException : Exception
{
    public AiServiceException(string message, string technicalDetails) : base(message) => TechnicalDetails = technicalDetails;

    /// <summary>For the server log and the AI usage log only, never for the browser.</summary>
    public string TechnicalDetails { get; }
}

/// <summary>
/// Calls the Anthropic Messages API (Claude) over HTTPS with HttpClient.
/// Settings come from the "AI" section of the configuration; the API key must
/// never be written into appsettings.json (see README, "AI assistant").
/// </summary>
public class ClaudeClient
{
    private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly HttpClient _http;
    private readonly string? _apiKey;

    public ClaudeClient(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _apiKey = configuration["AI:ApiKey"];
        Model = configuration["AI:Model"] ?? "claude-haiku-5-5";
        _http.BaseAddress = new Uri(configuration["AI:BaseUrl"] ?? "https://api.anthropic.com");
        _http.Timeout = TimeSpan.FromSeconds(configuration.GetValue("AI:TimeoutSeconds", 30));
    }

    public string Model { get; }

    /// <summary>True when an API key is configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public record Answer(string Text, int InputTokens, int OutputTokens, int LatencyMs);

    /// <summary>Sends one question with instructions and returns Claude's text answer.</summary>
    public async Task<Answer> AskAsync(string systemPrompt, string userMessage, int maxTokens = 700)
    {
        if (!IsConfigured)
        {
            throw new AiServiceException("The AI assistant is not configured on this server.", "No AI:ApiKey configured.");
        }

        var body = new
        {
            model = Model,
            max_tokens = maxTokens,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = userMessage } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = JsonContent.Create(body, options: Json)
        };
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var timer = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (TaskCanceledException)
        {
            throw new AiServiceException("The AI service took too long to answer. Please try again.", "Timeout");
        }
        catch (HttpRequestException ex)
        {
            throw new AiServiceException("The AI service could not be reached. Please try again later.", ex.Message);
        }

        using (response)
        {
            var json = await response.Content.ReadAsStringAsync();
            var latency = (int)timer.ElapsedMilliseconds;

            if (!response.IsSuccessStatusCode)
            {
                var userMessageText = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        "The AI service rejected the server's API key. Please tell the administrator.",
                    HttpStatusCode.TooManyRequests => "The AI service is busy right now. Please wait a moment and try again.",
                    _ => "The AI service returned an error. Please try again later."
                };
                throw new AiServiceException(userMessageText, $"HTTP {(int)response.StatusCode}: {Shorten(json)}");
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var text = string.Concat(root.GetProperty("content").EnumerateArray()
                    .Where(block => block.GetProperty("type").GetString() == "text")
                    .Select(block => block.GetProperty("text").GetString()));
                var usage = root.GetProperty("usage");
                return new Answer(text.Trim(), usage.GetProperty("input_tokens").GetInt32(),
                    usage.GetProperty("output_tokens").GetInt32(), latency);
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                throw new AiServiceException("The AI service returned an answer that could not be read.", Shorten(json));
            }
        }
    }

    private static string Shorten(string text) => text.Length > 300 ? text[..300] : text;
}
