using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Shared.Models;
using InterviewTranslator.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Whisper.net;
using Whisper.net.Ggml;

namespace InterviewTranslator.Stt;

public sealed class WhisperCppSttEngine : ISttEngine, IAsyncDisposable
{
    private readonly SttOptions _options;
    private readonly ILogger<WhisperCppSttEngine> _logger;

    private WhisperFactory?   _factory;
    private WhisperProcessor? _processor;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string _contextPrompt = "";

    public WhisperCppSttEngine(IOptions<AppOptions> options, ILogger<WhisperCppSttEngine> logger)
    {
        _options = options.Value.Stt;
        _logger  = logger;
    }

    public async Task<TranscriptionResult> TranscribeAsync(SpeechSegment segment, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _factory ??= await EnsureModelAsync(cancellationToken);

            // Her çağrıda önceki transcript'i prompt olarak vererek kısa segmentlerde bağlam sağla
            if (_processor is not null) { await _processor.DisposeAsync(); _processor = null; }
            var builder = _factory.CreateBuilder()
                .WithLanguage(_options.Language)
                .WithTemperature(_options.Temperature)
                .WithNoSpeechThreshold(_options.NoSpeechThreshold);
            if (!string.IsNullOrWhiteSpace(_contextPrompt))
                builder = builder.WithPrompt(_contextPrompt);
            _processor = builder.Build();

            var samples = ConvertPcm16ToFloat(segment.Pcm16Mono);
            var sb = new System.Text.StringBuilder();
            await foreach (var seg in _processor.ProcessAsync(samples, cancellationToken))
                sb.Append(seg.Text);

            var text = FilterHallucination(sb.ToString().Trim());
            _logger.LogInformation("STT [EN]: {Text}", text ?? "<sessizlik>");

            if (!string.IsNullOrWhiteSpace(text))
                _contextPrompt = text.Length > 224 ? text[^224..] : text;

            return new TranscriptionResult
            {
                SegmentId      = segment.Id,
                Text           = text ?? string.Empty,
                SourceLanguage = _options.Language,
                IsFinal        = true
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ResetAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_processor is not null) { await _processor.DisposeAsync(); _processor = null; }
            _factory?.Dispose();
            _factory = null;
            _contextPrompt = "";
            _logger.LogInformation("[EN STT] Processor sıfırlandı.");
        }
        finally { _lock.Release(); }
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
            if (fi.Exists)
            {
                _logger.LogWarning("EN model bozuk ({Size} MB), yeniden indiriliyor.", fi.Length / 1_048_576);
                File.Delete(modelPath);
            }
            else
            {
                _logger.LogInformation("EN model bulunamadı, indiriliyor: {Path}", modelPath);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
            await using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.TinyEn);
            await using var fileStream  = File.Create(modelPath);
            await modelStream.CopyToAsync(fileStream, ct);
            _logger.LogInformation("EN model indirildi: {Path}", modelPath);
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
        if (_processor is not null) await _processor.DisposeAsync();
        _factory?.Dispose();
        _lock.Dispose();
    }
}
