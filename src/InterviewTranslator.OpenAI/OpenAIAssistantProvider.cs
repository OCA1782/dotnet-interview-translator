using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Shared.Models;
using InterviewTranslator.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewTranslator.OpenAI;

public sealed class OpenAIAssistantProvider : IInterviewAssistantProvider
{
    private const string ChatCompletionsUrl = "https://api.openai.com/v1/chat/completions";

    private static readonly string SystemPrompt = """
        You are an interview assistant for a Turkish software engineer attending an English job interview.
        Analyze the interview question provided in both English (original) and Turkish (translation).
        Return ONLY valid JSON with this exact structure, no markdown, no explanation:
        {
          "summary": "<1-2 sentence Turkish summary of what was asked>",
          "intent": "<exactly one of: teknik|davranışsal|deneyim|maaş|genel>",
          "answerKey": "<short English phrase describing the answer topic, e.g. 'microservices tradeoffs'>",
          "confidence": <0.0 to 1.0>,
          "warnings": []
        }
        Rules:
        - summary must be in Turkish
        - intent must be exactly one of the five options
        - confidence reflects how clear the question was (0.9=clear, 0.5=ambiguous)
        - warnings: add a string if translation seems wrong or question is unclear
        - Technical terms (CQRS, Docker, Redis, etc.) must NOT be translated in summary
        """;

    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<AppOptions> _options;
    private readonly ILogger<OpenAIAssistantProvider> _logger;

    public OpenAIAssistantProvider(
        IHttpClientFactory httpFactory,
        IOptionsMonitor<AppOptions> options,
        ILogger<OpenAIAssistantProvider> logger)
    {
        _httpFactory = httpFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<InterviewAssistResult> AnalyzeQuestionAsync(
        InterviewAssistRequest request,
        CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue.OpenAI;
        if (opts.Mode == OpenAIMode.Disabled || string.IsNullOrWhiteSpace(opts.ApiKey))
            return InterviewAssistResult.Empty;

        if (string.IsNullOrWhiteSpace(request.OriginalEnglish)
            && string.IsNullOrWhiteSpace(request.TranslatedTurkish))
            return InterviewAssistResult.Empty;

        try
        {
            var userContent = $"""
                English (original): {request.OriginalEnglish}
                Turkish (translation): {request.TranslatedTurkish}
                Domain: {request.Domain}
                """;

            var body = new
            {
                model = opts.AssistantModel,
                max_tokens = opts.MaxTokens,
                temperature = 0.2,
                messages = new[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user",   content = userContent }
                }
            };

            var http = _httpFactory.CreateClient("openai");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(opts.TimeoutSeconds));

            using var response = await http.PostAsJsonAsync(ChatCompletionsUrl, body, cts.Token);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cts.Token);
            return ParseResponse(json);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[OpenAI] Zaman aşımı — assistant sonucu yoksayıldı");
            return InterviewAssistResult.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OpenAI] Assistant hatası");
            return InterviewAssistResult.Empty;
        }
    }

    private static InterviewAssistResult ParseResponse(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        using var inner = JsonDocument.Parse(content);
        var r = inner.RootElement;

        var warnings = new List<string>();
        if (r.TryGetProperty("warnings", out var wArr))
            foreach (var w in wArr.EnumerateArray())
                warnings.Add(w.GetString() ?? "");

        return new InterviewAssistResult
        {
            TurkishSummary     = r.TryGetProperty("summary",    out var s) ? s.GetString() ?? "" : "",
            DetectedIntent     = r.TryGetProperty("intent",     out var i) ? i.GetString() ?? "genel" : "genel",
            SuggestedAnswerKey = r.TryGetProperty("answerKey",  out var a) ? a.GetString() ?? "" : "",
            Confidence         = r.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0.0,
            Warnings           = warnings,
        };
    }
}
