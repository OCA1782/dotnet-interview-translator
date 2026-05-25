using InterviewTranslator.Shared.Models;
using InterviewTranslator.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Whisper.net;
using Whisper.net.Ggml;

namespace InterviewTranslator.Stt;

public sealed class MicWhisperSttEngine : IAsyncDisposable
{
    private readonly SttOptions _options;
    private readonly ILogger<MicWhisperSttEngine> _logger;

    // İki bağımsız factory + processor — TR transkript ve TR→EN çeviri paralel çalışır
    private WhisperFactory?   _transcribeFactory;
    private WhisperProcessor? _transcribeProcessor;
    private readonly SemaphoreSlim _transcribeLock = new(1, 1);

    private WhisperFactory?   _translateFactory;
    private WhisperProcessor? _translateProcessor;
    private readonly SemaphoreSlim _translateLock = new(1, 1);

    public MicWhisperSttEngine(IOptions<AppOptions> options, ILogger<MicWhisperSttEngine> logger)
    {
        _options = options.Value.MicStt;
        _logger  = logger;
    }

    public async Task<string> TranscribeToTurkishAsync(SpeechSegment segment, CancellationToken ct)
    {
        await _transcribeLock.WaitAsync(ct);
        try
        {
            await EnsureTranscribeAsync(ct);
            var sb = new System.Text.StringBuilder();
            await foreach (var seg in _transcribeProcessor!.ProcessAsync(ConvertPcm16ToFloat(segment.Pcm16Mono), ct))
                sb.Append(seg.Text);
            var text = FilterHallucination(sb.ToString().Trim());
            _logger.LogInformation("[MIC] TR: {T}", text ?? "<sessizlik>");
            return text ?? string.Empty;
        }
        finally { _transcribeLock.Release(); }
    }

    public async Task<string> TranslateToEnglishAsync(SpeechSegment segment, CancellationToken ct)
    {
        await _translateLock.WaitAsync(ct);
        try
        {
            await EnsureTranslateAsync(ct);
            var sb = new System.Text.StringBuilder();
            await foreach (var seg in _translateProcessor!.ProcessAsync(ConvertPcm16ToFloat(segment.Pcm16Mono), ct))
                sb.Append(seg.Text);
            var text = FilterHallucination(sb.ToString().Trim());
            _logger.LogInformation("[MIC] TR→EN: {T}", text ?? "<sessizlik>");
            return text ?? string.Empty;
        }
        finally { _translateLock.Release(); }
    }

    private async Task EnsureTranscribeAsync(CancellationToken ct)
    {
        if (_transcribeProcessor is not null) return;
        _transcribeFactory = await EnsureModelAsync(ct);
        var builder = _transcribeFactory.CreateBuilder()
            .WithTemperature(_options.Temperature)
            .WithNoSpeechThreshold(_options.NoSpeechThreshold);
        // "auto" → Whisper kendi dil tespitini yapar; zorunlu dil atanmaz
        if (!string.Equals(_options.Language, "auto", StringComparison.OrdinalIgnoreCase))
            builder = builder.WithLanguage(_options.Language);
        _transcribeProcessor = builder.Build();
        _logger.LogInformation("[MIC] Transkript processor hazır (model={M} lang={L})",
            _options.ModelType, _options.Language);
    }

    private async Task EnsureTranslateAsync(CancellationToken ct)
    {
        if (_translateProcessor is not null) return;
        _translateFactory   = await EnsureModelAsync(ct);
        _translateProcessor = _translateFactory.CreateBuilder()
            .WithTranslate()
            .WithTemperature(_options.Temperature)
            .WithNoSpeechThreshold(_options.NoSpeechThreshold)
            .Build();
        _logger.LogInformation("[MIC] TR→EN çeviri processor hazır.");
    }

    /// <summary>Model değişince processor'ları sıfırlar.</summary>
    public async Task ResetAsync()
    {
        await _transcribeLock.WaitAsync();
        await _translateLock.WaitAsync();
        try
        {
            if (_transcribeProcessor is not null) { await _transcribeProcessor.DisposeAsync(); _transcribeProcessor = null; }
            if (_translateProcessor  is not null) { await _translateProcessor.DisposeAsync();  _translateProcessor  = null; }
            _transcribeFactory?.Dispose(); _transcribeFactory = null;
            _translateFactory?.Dispose();  _translateFactory  = null;
            _logger.LogInformation("[MIC] Processor'lar sıfırlandı.");
        }
        finally
        {
            _translateLock.Release();
            _transcribeLock.Release();
        }
    }

    private async Task<WhisperFactory> EnsureModelAsync(CancellationToken ct)
    {
        var modelPath = Path.IsPathRooted(_options.ModelPath)
            ? _options.ModelPath
            : Path.Combine(AppContext.BaseDirectory, _options.ModelPath);

        const long MinSize = 35 * 1024 * 1024;
        var fi = new FileInfo(modelPath);
        if (!fi.Exists || fi.Length < MinSize)
        {
            if (fi.Exists) File.Delete(modelPath);
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
            _logger.LogInformation("[MIC] Model indiriliyor: {Path}", modelPath);
            await using var ms = await WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.Tiny);
            await using var fs = File.Create(modelPath);
            await ms.CopyToAsync(fs, ct);
            _logger.LogInformation("[MIC] Model indirildi.");
        }
        return WhisperFactory.FromPath(modelPath);
    }

    private static readonly HashSet<string> _knownHallucinations = new(StringComparer.OrdinalIgnoreCase)
    {
        "[blank_audio]", "[music]", "[applause]", "[silence]",
        "(music)", "(applause)", "(silence)",
        "thank you.", "thank you!", "thanks for watching.",
        "thank you for watching.", "thank you for watching!",
        "you", ".", "...", ". . .", "…"
    };

    private static string? FilterHallucination(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (_knownHallucinations.Contains(text.Trim())) return null;
        var words    = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return null;
        var maxCount = words.GroupBy(w => w.ToLowerInvariant()).Max(g => g.Count());
        if ((double)maxCount / words.Length > 0.5 && words.Length > 3) return null;
        return text;
    }

    private static float[] ConvertPcm16ToFloat(byte[] pcm16)
    {
        var samples = new float[pcm16.Length / 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short s    = (short)(pcm16[i * 2] | (pcm16[i * 2 + 1] << 8));
            samples[i] = s / 32768f;
        }
        return samples;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transcribeProcessor is not null) await _transcribeProcessor.DisposeAsync();
        if (_translateProcessor  is not null) await _translateProcessor.DisposeAsync();
        _transcribeFactory?.Dispose();
        _translateFactory?.Dispose();
        _transcribeLock.Dispose();
        _translateLock.Dispose();
    }
}
