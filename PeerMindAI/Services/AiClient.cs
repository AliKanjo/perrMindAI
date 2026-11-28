using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace PeerMindAI.Services;

/// <summary>The AI service failed; the message is safe to show to the user.</summary>
public class AiServiceException : Exception
{
    public AiServiceException(string message, string technicalDetails) : base(message) => TechnicalDetails = technicalDetails;

    /// <summary>For the server log and the AI usage log only, never for the browser.</summary>
    public string TechnicalDetails { get; }
}

/// <summary>
/// Sends one prompt to the AI service and returns the text answer. Two providers are supported,
/// chosen with the "AI:Provider" setting:
/// <list type="bullet">
/// <item><b>Anthropic</b> – Claude, through the Anthropic Messages API (<c>/v1/messages</c>).</item>
/// <item><b>Groq</b> – open models such as gpt-oss, Qwen or Llama, through Groq's OpenAI-compatible API
/// (<c>/openai/v1/chat/completions</c>). Groq has a free tier.</item>
/// </list>
/// The API key must never be written into appsettings.json (see README, "AI features").
/// </summary>
public class AiClient
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly bool _isGroq;

    public AiClient(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _apiKey = configuration["AI:ApiKey"];
        _isGroq = string.Equals(configuration["AI:Provider"], "Groq", StringComparison.OrdinalIgnoreCase);
        Provider = _isGroq ? "Groq" : "Anthropic";

        // each provider has its own default address and model; both can be changed in the settings
        Model = configuration["AI:Model"] is { Length: > 0 } model ? model
              : _isGroq ? "openai/gpt-oss-120b" : "claude-haiku-5-5";
        var baseUrl = configuration["AI:BaseUrl"] is { Length: > 0 } url ? url
                    : _isGroq ? "https://api.groq.com" : "https://api.anthropic.com";
        _http.BaseAddress = new Uri(baseUrl);
        _http.Timeout = TimeSpan.FromSeconds(configuration.GetValue("AI:TimeoutSeconds", 30));
    }

    public string Provider { get; }

    public string Model { get; }

    /// <summary>True when an API key is configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public record Answer(string Text, int InputTokens, int OutputTokens, int LatencyMs);

    /// <summary>
    /// Sends instructions and one user message and returns the text answer.
    /// With <paramref name="jsonOnly"/> the answer must be a JSON object (used for quizzes).
    /// </summary>
    public async Task<Answer> AskAsync(string systemPrompt, string userMessage, int maxTokens = 700, bool jsonOnly = false)
    {
        if (!IsConfigured)
        {
            throw new AiServiceException("The AI features are not configured on this server.", "No AI:ApiKey configured.");
        }

        using var request = _isGroq
            ? GroqRequest(systemPrompt, userMessage, maxTokens, jsonOnly)
            : AnthropicRequest(systemPrompt, userMessage, maxTokens);

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
                var message = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        "The AI service rejected the server's API key. Please tell the administrator.",
                    HttpStatusCode.TooManyRequests => "The AI service is busy right now. Please wait a moment and try again.",
                    _ => "The AI service returned an error. Please try again later."
                };
                throw new AiServiceException(message, $"{Provider} HTTP {(int)response.StatusCode}: {Shorten(json)}");
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                var (text, inputTokens, outputTokens) = _isGroq ? ReadGroqAnswer(document.RootElement) : ReadAnthropicAnswer(document.RootElement);
                return new Answer(text.Trim(), inputTokens, outputTokens, latency);
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
            {
                throw new AiServiceException("The AI service returned an answer that could not be read.", Shorten(json));
            }
        }
    }

    // ---------- Anthropic Messages API ----------

    private HttpRequestMessage AnthropicRequest(string systemPrompt, string userMessage, int maxTokens)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = JsonContent.Create(new
            {
                model = Model,
                max_tokens = maxTokens,
                system = systemPrompt,
                messages = new[] { new { role = "user", content = userMessage } }
            })
        };
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        return request;
    }

    private static (string, int, int) ReadAnthropicAnswer(JsonElement root)
    {
        var text = string.Concat(root.GetProperty("content").EnumerateArray()
            .Where(block => block.GetProperty("type").GetString() == "text")
            .Select(block => block.GetProperty("text").GetString()));
        var usage = root.GetProperty("usage");
        return (text, usage.GetProperty("input_tokens").GetInt32(), usage.GetProperty("output_tokens").GetInt32());
    }

    // ---------- Groq (OpenAI-compatible chat completions) ----------

    // Reasoning models (gpt-oss, Qwen 3) think before they answer, and on Groq that thinking counts
    // toward the token limit, so they get extra room and are asked to keep the thinking short or hidden.
    private const int ReasoningAllowance = 2000;

    private HttpRequestMessage GroqRequest(string systemPrompt, string userMessage, int maxTokens, bool jsonOnly)
    {
        var model = Model.ToLowerInvariant();
        var body = new Dictionary<string, object>
        {
            ["model"] = Model,
            ["max_tokens"] = maxTokens + ReasoningAllowance,
            ["temperature"] = 0.3,
            ["messages"] = new[] { new { role = "system", content = systemPrompt }, new { role = "user", content = userMessage } }
        };
        if (jsonOnly)
        {
            body["response_format"] = new { type = "json_object" };
        }
        if (model.Contains("gpt-oss"))
        {
            body["reasoning_effort"] = "low";
        }
        else if (model.Contains("qwen3"))
        {
            body["reasoning_format"] = "hidden";
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/openai/v1/chat/completions") { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        return request;
    }

    private static (string, int, int) ReadGroqAnswer(JsonElement root)
    {
        var text = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        // some reasoning models put their thinking in the answer between <think> tags: keep only the answer
        var thinkEnd = text.LastIndexOf("</think>", StringComparison.Ordinal);
        if (thinkEnd >= 0)
        {
            text = text[(thinkEnd + "</think>".Length)..];
        }
        var usage = root.GetProperty("usage");
        return (text, usage.GetProperty("prompt_tokens").GetInt32(), usage.GetProperty("completion_tokens").GetInt32());
    }

    private static string Shorten(string text) => text.Length > 300 ? text[..300] : text;
}
