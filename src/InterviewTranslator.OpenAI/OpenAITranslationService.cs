using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Shared.Models;
using InterviewTranslator.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewTranslator.OpenAI;

public sealed class OpenAITranslationService : ITranslationService
{
    private const string ChatCompletionsUrl = "https://api.openai.com/v1/chat/completions";

    private static readonly string SystemPrompt =
        "You are a professional translator. Translate the English text to Turkish. " +
        "Keep technical terms (Docker, Kubernetes, API, CQRS, etc.) in English. " +
        "Return ONLY the translation — no explanations, no markdown.";

    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<AppOptions> _options;
    private readonly ILogger<OpenAITranslationService> _logger;

    public OpenAITranslationService(
        IHttpClientFactory httpFactory,
        IOptionsMonitor<AppOptions> options,
        ILogger<OpenAITranslationService> logger)
    {
        _httpFactory = httpFactory;
        _options     = options;
        _logger      = logger;
    }

    public async Task<TranslationResult> TranslateAsync(TranscriptionResult transcript, CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue.OpenAI;

        if (string.IsNullOrWhiteSpace(transcript.Text))
            return Empty(transcript);

        try
        {
            var body = new
            {
                model       = opts.AssistantModel,
                max_tokens  = 300,
                temperature = 0.1,
                messages    = new[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user",   content = transcript.Text }
                }
            };

            var http = _httpFactory.CreateClient("openai");

            using var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsUrl)
            {
                Content = JsonContent.Create(body)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opts.ApiKey);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(opts.TimeoutSeconds));

            using var response = await http.SendAsync(request, cts.Token);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(json);
            var translated = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()?.Trim() ?? "";

            _logger.LogInformation("[OpenAI Translate] {TR}", translated);

            return new TranslationResult
            {
                SegmentId      = transcript.SegmentId,
                SourceText     = transcript.Text,
                TranslatedText = translated,
                SourceLanguage = "en",
                TargetLanguage = "tr"
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[OpenAI Translate] Zaman aşımı");
            return Empty(transcript);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OpenAI Translate] Hata");
            return Empty(transcript);
        }
    }

    private static TranslationResult Empty(TranscriptionResult t) => new()
    {
        SegmentId      = t.SegmentId,
        SourceText     = t.Text,
        TranslatedText = "",
        SourceLanguage = "en",
        TargetLanguage = "tr"
    };
}
