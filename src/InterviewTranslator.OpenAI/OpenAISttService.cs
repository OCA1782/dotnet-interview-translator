using System.Net.Http.Headers;
using System.Text.Json;
using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Shared.Models;
using InterviewTranslator.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewTranslator.OpenAI;

public sealed class OpenAISttService : ISttEngine
{
    private const string TranscriptionsUrl = "https://api.openai.com/v1/audio/transcriptions";

    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<AppOptions> _options;
    private readonly ILogger<OpenAISttService> _logger;

    public OpenAISttService(
        IHttpClientFactory httpFactory,
        IOptionsMonitor<AppOptions> options,
        ILogger<OpenAISttService> logger)
    {
        _httpFactory = httpFactory;
        _options     = options;
        _logger      = logger;
    }

    public async Task<TranscriptionResult> TranscribeAsync(SpeechSegment segment, CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue.OpenAI;

        try
        {
            var wav = BuildWav(segment.Pcm16Mono, segment.SampleRate > 0 ? segment.SampleRate : 16000);

            using var content = new MultipartFormDataContent();

            var audioContent = new ByteArrayContent(wav);
            audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            content.Add(audioContent, "file", "audio.wav");
            content.Add(new StringContent("whisper-1"), "model");
            content.Add(new StringContent("en"), "language");

            var http = _httpFactory.CreateClient("openai");

            // API anahtarı çalışma zamanında değişebilir — her istekte taze oku
            using var request = new HttpRequestMessage(HttpMethod.Post, TranscriptionsUrl) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opts.ApiKey);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(opts.TimeoutSeconds));

            using var response = await http.SendAsync(request, cts.Token);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(json);
            var text = doc.RootElement.GetProperty("text").GetString()?.Trim() ?? "";

            _logger.LogInformation("[OpenAI STT] {Text}", text);

            return new TranscriptionResult
            {
                SegmentId      = segment.Id,
                Text           = text,
                SourceLanguage = "en",
                IsFinal        = true
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[OpenAI STT] Zaman aşımı — segment yoksayıldı");
            return new TranscriptionResult { SegmentId = segment.Id, Text = "", IsFinal = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OpenAI STT] Hata — local STT'ye düşülmeli");
            return new TranscriptionResult { SegmentId = segment.Id, Text = "", IsFinal = true };
        }
    }

    public Task ResetAsync() => Task.CompletedTask;

    private static byte[] BuildWav(byte[] pcm16, int sampleRate)
    {
        int dataLen = pcm16.Length;
        var buf     = new byte[44 + dataLen];
        var s       = buf.AsSpan();

        "RIFF"u8.CopyTo(s[0..]);
        BitConverter.TryWriteBytes(s[4..],  36 + dataLen);
        "WAVE"u8.CopyTo(s[8..]);
        "fmt "u8.CopyTo(s[12..]);
        BitConverter.TryWriteBytes(s[16..], 16);
        BitConverter.TryWriteBytes(s[20..], (short)1);           // PCM
        BitConverter.TryWriteBytes(s[22..], (short)1);           // mono
        BitConverter.TryWriteBytes(s[24..], sampleRate);
        BitConverter.TryWriteBytes(s[28..], sampleRate * 2);     // byte rate
        BitConverter.TryWriteBytes(s[32..], (short)2);           // block align
        BitConverter.TryWriteBytes(s[34..], (short)16);          // bits per sample
        "data"u8.CopyTo(s[36..]);
        BitConverter.TryWriteBytes(s[40..], dataLen);
        pcm16.CopyTo(s[44..]);

        return buf;
    }
}
