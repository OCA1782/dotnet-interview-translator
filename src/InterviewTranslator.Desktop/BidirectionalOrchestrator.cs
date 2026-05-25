using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Shared.Models;
using InterviewTranslator.Stt;
using InterviewTranslator.Subtitles;
using Microsoft.Extensions.Logging;

namespace InterviewTranslator.Desktop;

public sealed class BidirectionalOrchestrator
{
    private readonly IMicrophoneCaptureService _mic;
    private readonly IVadService _vad;
    private readonly MicWhisperSttEngine _stt;
    private readonly ISuggestionPublisher _publisher;
    private readonly ILogger<BidirectionalOrchestrator> _logger;

    public BidirectionalOrchestrator(
        IMicrophoneCaptureService mic,
        IVadService vad,
        MicWhisperSttEngine stt,
        ISuggestionPublisher publisher,
        ILogger<BidirectionalOrchestrator> logger)
    {
        _mic       = mic;
        _vad       = vad;
        _stt       = stt;
        _publisher = publisher;
        _logger    = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TR→EN orchestrator başladı.");
        var frames = _mic.CaptureAsync(cancellationToken);
        int busy = 0;

        await foreach (var segment in _vad.DetectSpeechAsync(frames, cancellationToken))
        {
            if (Interlocked.CompareExchange(ref busy, 1, 0) == 0)
            {
                var seg = segment;
                _ = Task.Run(async () =>
                {
                    try   { await ProcessSegmentAsync(seg, cancellationToken); }
                    finally { Volatile.Write(ref busy, 0); }
                }, cancellationToken);
            }
        }

        _logger.LogInformation("TR→EN orchestrator durdu.");
    }

    private async Task ProcessSegmentAsync(SpeechSegment segment, CancellationToken ct)
    {
        try
        {
            // Her iki Whisper geçişi paralel çalışır
            var trTask = _stt.TranscribeToTurkishAsync(segment, ct);
            var enTask = _stt.TranslateToEnglishAsync(segment, ct);
            await Task.WhenAll(trTask, enTask);
            var turkishText = trTask.Result;
            var englishText = enTask.Result;

            if (string.IsNullOrWhiteSpace(turkishText) && string.IsNullOrWhiteSpace(englishText))
                return;

            _logger.LogInformation("TR→EN: TR=[{TR}] EN=[{EN}]", turkishText, englishText);

            await _publisher.PublishAsync(new SuggestionItem
            {
                SegmentId         = segment.Id,
                TurkishText       = turkishText,
                EnglishSuggestion = englishText,
                CreatedAt         = DateTimeOffset.UtcNow
            }, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TR→EN segment hatası [{Id}]", segment.Id);
        }
    }
}
