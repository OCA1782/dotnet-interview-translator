using System.Speech.Synthesis;
using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewTranslator.Desktop;

public sealed class WindowsTtsService : ITtsService, IDisposable
{
    private readonly SpeechSynthesizer _synth = new();
    private readonly TtsOptions _options;
    private readonly ILogger<WindowsTtsService> _logger;

    public WindowsTtsService(IOptions<AppOptions> options, ILogger<WindowsTtsService> logger)
    {
        _options = options.Value.Tts;
        _logger = logger;
        ApplyOptions();
    }

    public void Speak(string text)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(text)) return;
        try
        {
            _synth.SpeakAsyncCancelAll();
            _synth.SpeakAsync(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TTS konuşma hatası.");
        }
    }

    public void Stop()
    {
        try { _synth.SpeakAsyncCancelAll(); }
        catch { /* ignore */ }
    }

    // SettingsWindow'dan çağrılır — options değiştiğinde uygula
    public void ApplyOptions()
    {
        _synth.Rate   = Math.Clamp(_options.Rate, -10, 10);
        _synth.Volume = Math.Clamp(_options.Volume, 0, 100);
    }

    public void Dispose() => _synth.Dispose();
}
