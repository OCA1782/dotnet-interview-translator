using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using InterviewTranslator.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewTranslator.Desktop;

public sealed class GitHubDocsService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<AppOptions> _appOptions;
    private readonly ILogger<GitHubDocsService> _logger;

    private List<AnswerCard>? _cached;
    private DateTimeOffset _lastFetch = DateTimeOffset.MinValue;

    public string StatusText => _lastFetch == DateTimeOffset.MinValue
        ? "Yerel veriler"
        : $"GitHub · {_lastFetch.LocalDateTime:HH:mm} · {_cached?.Count ?? 0} kart";

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GitHubDocsService(
        IHttpClientFactory httpFactory,
        IOptionsMonitor<AppOptions> appOptions,
        ILogger<GitHubDocsService> logger)
    {
        _httpFactory = httpFactory;
        _appOptions  = appOptions;
        _logger      = logger;
    }

    public IReadOnlyList<AnswerCard> GetCards() =>
        _cached ?? AnswerCardRepository.All;

    public async Task<IReadOnlyList<AnswerCard>> RefreshAsync(CancellationToken ct = default)
    {
        var baseUrl = _appOptions.CurrentValue.GitHubDocs.RawBaseUrl.TrimEnd('/');
        var url     = $"{baseUrl}/answer-cards.json";

        try
        {
            var http = _httpFactory.CreateClient("github");
            _logger.LogInformation("[GitHub Docs] Kartlar yükleniyor: {Url}", url);

            var json   = await http.GetStringAsync(url, ct);
            var doc    = JsonSerializer.Deserialize<AnswerCardsDocument>(json, _json);
            var cards  = doc?.Cards
                .Select(c => new AnswerCard
                {
                    Category = c.Category,
                    Topic    = c.Topic,
                    Answer   = c.Answer
                })
                .ToList() ?? [];

            _cached    = cards;
            _lastFetch = DateTimeOffset.UtcNow;
            _logger.LogInformation("[GitHub Docs] {Count} kart yüklendi (v{Ver})",
                cards.Count, doc?.Version ?? "?");
            return _cached;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[GitHub Docs] Yüklenemedi — yerel veriler kullanılıyor");
            return _cached ?? AnswerCardRepository.All;
        }
    }

    // JSON modelleri
    private sealed class AnswerCardsDocument
    {
        public string Version { get; init; } = "";
        public List<CardDto> Cards { get; init; } = [];
    }

    private sealed class CardDto
    {
        public string Category { get; init; } = "";
        public string Topic    { get; init; } = "";
        public string Answer   { get; init; } = "";
    }
}
