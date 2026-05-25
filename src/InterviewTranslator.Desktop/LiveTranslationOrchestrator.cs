using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Shared.Models;
using InterviewTranslator.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewTranslator.Desktop;

public sealed class LiveTranslationOrchestrator
{
    private readonly IAudioCaptureService _audio;
    private readonly IVadService _vad;
    private readonly ISttEngine _stt;
    private readonly ITranslationService _translation;
    private readonly ISubtitlePublisher _subtitlePublisher;
    private readonly IInterviewAssistantProvider _assistant;
    private readonly IOptionsMonitor<AppOptions> _appOptions;
    private readonly ILogger<LiveTranslationOrchestrator> _logger;

    public LiveTranslationOrchestrator(
        IAudioCaptureService audio,
        IVadService vad,
        ISttEngine stt,
        ITranslationService translation,
        ISubtitlePublisher subtitlePublisher,
        IInterviewAssistantProvider assistant,
        IOptionsMonitor<AppOptions> appOptions,
        ILogger<LiveTranslationOrchestrator> logger)
    {
        _audio = audio;
        _vad = vad;
        _stt = stt;
        _translation = translation;
        _subtitlePublisher = subtitlePublisher;
        _assistant = assistant;
        _appOptions = appOptions;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("EN→TR orchestrator başladı.");
        var frames = _audio.CaptureAsync(cancellationToken);
        int segNo = 0;
        int busy = 0; // STT meşgulken yeni segment gelirse atla — gecikme birikimini önler

        await foreach (var segment in _vad.DetectSpeechAsync(frames, cancellationToken))
        {
            _logger.LogInformation("EN segment #{No} — {Dur:F1}s", ++segNo, segment.Duration.TotalSeconds);
            if (Interlocked.CompareExchange(ref busy, 1, 0) == 0)
            {
                var seg = segment;
                _ = Task.Run(async () =>
                {
                    try   { await ProcessSegmentAsync(seg, cancellationToken); }
                    finally { Volatile.Write(ref busy, 0); }
                }, cancellationToken);
            }
            else
            {
                _logger.LogDebug("EN segment #{No} atlandı — STT meşgul.", segNo);
            }
        }

        _logger.LogInformation("EN→TR orchestrator durdu.");
    }

    private async Task ProcessSegmentAsync(SpeechSegment segment, CancellationToken ct)
    {
        try
        {
            // Adım 1: Whisper STT → hemen İngilizce göster
            var transcript = await _stt.TranscribeAsync(segment, ct);
            if (string.IsNullOrWhiteSpace(transcript.Text)) return;

            _logger.LogInformation("STT [EN]: {Text}", transcript.Text);

            // İngilizce hemen yayınla (Türkçe bekleme olmadan)
            await _subtitlePublisher.PublishAsync(new SubtitleItem
            {
                SegmentId   = segment.Id,
                EnglishText = transcript.Text,
                TurkishText = "",
                CreatedAt   = DateTimeOffset.UtcNow,
                Latency     = DateTimeOffset.UtcNow - segment.StartedAt
            }, ct);

            // Adım 2: LibreTranslate → Türkçe çeviriyi güncelle
            var translation = await _translation.TranslateAsync(transcript, ct);
            if (string.IsNullOrWhiteSpace(translation.TranslatedText)
                || translation.TranslatedText == transcript.Text)
                return;

            _logger.LogInformation("Çeviri [EN→TR]: {TR}", translation.TranslatedText);

            await _subtitlePublisher.PublishAsync(new SubtitleItem
            {
                SegmentId   = segment.Id,
                EnglishText = transcript.Text,
                TurkishText = translation.TranslatedText,
                CreatedAt   = DateTimeOffset.UtcNow,
                Latency     = DateTimeOffset.UtcNow - segment.StartedAt
            }, ct);

            // Adım 3: OpenAI assistant katmanı (opsiyonel — AssistantOnly veya üzeri mod)
            var openAiMode = _appOptions.CurrentValue.OpenAI.Mode;
            if (openAiMode != OpenAIMode.Disabled)
            {
                _ = Task.Run(() => RunAssistantAsync(transcript.Text, translation.TranslatedText, ct), ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EN→TR segment hatası [{Id}]", segment.Id);
        }
    }

    private async Task RunAssistantAsync(string english, string turkish, CancellationToken ct)
    {
        try
        {
            var result = await _assistant.AnalyzeQuestionAsync(new InterviewAssistRequest
            {
                OriginalEnglish   = english,
                TranslatedTurkish = turkish,
            }, ct);

            if (string.IsNullOrWhiteSpace(result.TurkishSummary)) return;

            _logger.LogInformation(
                "[Assistant] Intent={Intent} Conf={Conf:P0} | {Summary}",
                result.DetectedIntent, result.Confidence, result.TurkishSummary);

            AssistantResultPublished?.Invoke(result);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Assistant] Hata");
        }
    }

    /// UI katmanı bu event'i dinleyerek assistant sonuçlarını gösterir.
    public event Action<InterviewAssistResult>? AssistantResultPublished;
}
