using System.Net.Http.Json;
using System.Text.Json.Serialization;
using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Shared.Models;
using InterviewTranslator.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewTranslator.Translate;

public sealed class LibreTranslateService : ITranslationService
{
    private readonly HttpClient _http;
    private readonly TranslationOptions _options;
    private readonly GlossaryService _glossary;
    private readonly ILogger<LibreTranslateService> _logger;

    public LibreTranslateService(
        HttpClient http,
        IOptions<AppOptions> options,
        GlossaryService glossary,
        ILogger<LibreTranslateService> logger)
    {
        _http = http;
        _options = options.Value.Translation;
        _glossary = glossary;
        _logger = logger;
    }

    public async Task<TranslationResult> TranslateAsync(TranscriptionResult transcript, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transcript.Text))
            return EmptyResult(transcript);

        var protectedText = _glossary.Protect(transcript.Text);

        try
        {
            var request = new TranslateRequest
            {
                Q      = protectedText,
                Source = _options.SourceLanguage,
                Target = _options.TargetLanguage
            };

            var response = await _http.PostAsJsonAsync("/translate", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result     = await response.Content.ReadFromJsonAsync<TranslateResponse>(cancellationToken: cancellationToken);
            var translated = _glossary.Restore((result?.TranslatedText ?? "").Trim());

            _logger.LogInformation("Çeviri [EN→TR]: {TR}", translated);

            return new TranslationResult
            {
                SegmentId      = transcript.SegmentId,
                SourceText     = transcript.Text,
                TranslatedText = translated,
                SourceLanguage = _options.SourceLanguage,
                TargetLanguage = _options.TargetLanguage
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Çeviri başarısız.");
            return new TranslationResult
            {
                SegmentId      = transcript.SegmentId,
                SourceText     = transcript.Text,
                TranslatedText = transcript.Text,
                SourceLanguage = _options.SourceLanguage,
                TargetLanguage = _options.TargetLanguage
            };
        }
    }

    private static TranslationResult EmptyResult(TranscriptionResult t) => new()
    {
        SegmentId = t.SegmentId,
        SourceText = t.Text,
        TranslatedText = "",
        SourceLanguage = "en",
        TargetLanguage = "tr"
    };

    private sealed class TranslateRequest
    {
        [JsonPropertyName("q")] public string Q { get; set; } = "";
        [JsonPropertyName("source")] public string Source { get; set; } = "en";
        [JsonPropertyName("target")] public string Target { get; set; } = "tr";
        [JsonPropertyName("format")] public string Format { get; set; } = "text";
    }

    private sealed class TranslateResponse
    {
        [JsonPropertyName("translatedText")] public string TranslatedText { get; set; } = "";
    }
}
