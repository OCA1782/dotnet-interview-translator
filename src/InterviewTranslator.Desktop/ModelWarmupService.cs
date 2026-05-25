using System.IO;
using InterviewTranslator.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Whisper.net;
using Whisper.net.Ggml;

namespace InterviewTranslator.Desktop;

/// <summary>
/// Uygulama başladığında modelleri arka planda indirir / doğrular.
/// İlk konuşmada bekleme olmaz.
/// </summary>
public sealed class ModelWarmupService
{
    private readonly AppOptions _opts;
    private readonly ILogger<ModelWarmupService> _logger;

    public event Action<string>? StatusChanged;

    public ModelWarmupService(IOptions<AppOptions> options, ILogger<ModelWarmupService> logger)
    {
        _opts = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
        => Task.Run(() => RunAsync(cancellationToken), cancellationToken);

    private async Task RunAsync(CancellationToken ct)
    {
        await EnsureModelAsync(
            _opts.Stt.ModelPath,
            GgmlType.TinyEn,
            minSizeBytes: 35L * 1024 * 1024,
            label: "EN",
            ct);

        await EnsureModelAsync(
            _opts.MicStt.ModelPath,
            GgmlType.Tiny,
            minSizeBytes: 35L * 1024 * 1024,
            label: "TR",
            ct);

        StatusChanged?.Invoke("Modeller hazır ✓");
    }

    private async Task EnsureModelAsync(
        string relativePath, GgmlType type, long minSizeBytes, string label, CancellationToken ct)
    {
        var modelPath = Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(AppContext.BaseDirectory, relativePath);

        var fi = new FileInfo(modelPath);
        if (fi.Exists && fi.Length >= minSizeBytes)
        {
            _logger.LogInformation("Model [{Label}] hazır: {Size} MB", label, fi.Length / 1_048_576);
            StatusChanged?.Invoke($"Model [{label}] hazır ({fi.Length / 1_048_576} MB)");
            return;
        }

        if (fi.Exists)
        {
            _logger.LogWarning("Model [{Label}] bozuk ({Size} MB), siliniyor.", label, fi.Length / 1_048_576);
            File.Delete(modelPath);
        }

        _logger.LogInformation("Model [{Label}] indiriliyor...", label);
        StatusChanged?.Invoke($"Model [{label}] indiriliyor... (lütfen bekleyin)");

        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        try
        {
            await using var stream = await WhisperGgmlDownloader.GetGgmlModelAsync(type);
            await using var file   = File.Create(modelPath);
            await stream.CopyToAsync(file, ct);
            _logger.LogInformation("Model [{Label}] indirildi: {Path}", label, modelPath);
            StatusChanged?.Invoke($"Model [{label}] indirildi ✓");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Model [{Label}] indirilemedi.", label);
            StatusChanged?.Invoke($"Model [{label}] indirme BAŞARISIZ — internet bağlantısını kontrol edin.");
        }
    }
}
