using InterviewTranslator.Audio;
using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Shared.Options;
using InterviewTranslator.Stt;
using InterviewTranslator.Translate;
using InterviewTranslator.Vad;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

// FAZ 0 — Teknik Doğrulama Testi
// Başarı kriteri: 3-5 sn gecikmeyle TR + EN konsol çıktısı.

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var host = Host.CreateDefaultBuilder(args)
    .UseSerilog()
    .ConfigureServices((ctx, services) =>
    {
        services.Configure<AppOptions>(ctx.Configuration.GetSection("App"));
        services.AddAudioCapture();
        services.AddVad();
        services.AddStt();
        services.AddTranslation();
    })
    .Build();

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

Console.WriteLine("=== InterviewTranslator FAZ 0 — Konsol Testi ===");
Console.WriteLine("Sistem sesini dinliyor... Durdurmak için Ctrl+C\n");

var audio = host.Services.GetRequiredService<IAudioCaptureService>();
var vad   = host.Services.GetRequiredService<IVadService>();
var stt   = host.Services.GetRequiredService<ISttEngine>();
var trans = host.Services.GetRequiredService<ITranslationService>();

var devices = audio.GetOutputDevices();
Console.WriteLine("Mevcut ses cihazları:");
for (int i = 0; i < devices.Count; i++)
    Console.WriteLine($"  [{i}] {devices[i].Name}");

if (devices.Count > 1)
{
    Console.Write($"\nCihaz seçin [0-{devices.Count - 1}] (Enter = 0): ");
    var input = Console.ReadLine();
    if (int.TryParse(input, out int idx) && idx >= 0 && idx < devices.Count)
        audio.SetDevice(devices[idx].Id);
}
Console.WriteLine();

int segmentCount = 0;

try
{
    var frames = audio.CaptureAsync(cts.Token);
    await foreach (var segment in vad.DetectSpeechAsync(frames, cts.Token))
    {
        segmentCount++;
        var num = segmentCount;
        _ = Task.Run(async () =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var transcript = await stt.TranscribeAsync(segment, cts.Token);
            var sttMs = sw.ElapsedMilliseconds;

            if (string.IsNullOrWhiteSpace(transcript.Text)) return;

            var translation = await trans.TranslateAsync(transcript, cts.Token);
            sw.Stop();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n[Segment #{num} | {segment.Duration.TotalSeconds:F1}s | STT:{sttMs}ms | Toplam:{sw.ElapsedMilliseconds}ms]");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  EN: {transcript.Text}");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  TR: {translation.TranslatedText}");
            Console.ResetColor();
        }, cts.Token);
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine($"\nTest sonlandırıldı. Toplam segment: {segmentCount}");
}
finally
{
    Log.CloseAndFlush();
}
