using System.IO;
using System.Windows;
using InterviewTranslator.Audio;
using InterviewTranslator.OpenAI;
using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Shared.Options;
using InterviewTranslator.Stt;
using InterviewTranslator.Subtitles;
using InterviewTranslator.Translate;
using InterviewTranslator.Vad;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace InterviewTranslator.Desktop;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        // Tüm thread'lerdeki exception'ları yakala ve logla
        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Fatal(ex.Exception, "[CRASH] UI thread exception");
            Log.CloseAndFlush();
            ex.Handled = true;
            MessageBox.Show($"Kritik hata:\n{ex.Exception.Message}\n\nDetay log dosyasında.",
                "Uygulama Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            Log.Fatal(ex.ExceptionObject as Exception, "[CRASH] Non-UI thread exception. IsTerminating={T}", ex.IsTerminating);
            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            Log.Error(ex.Exception, "[CRASH] Unobserved task exception");
            ex.SetObserved();
        };

        _host = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .UseSerilog()
            .ConfigureServices((ctx, services) =>
            {
                services.Configure<AppOptions>(ctx.Configuration.GetSection("App"));

                services.AddAudioCapture();
                services.AddVad();
                services.AddStt();
                services.AddTranslation();
                services.AddSubtitles();

                services.AddHttpClient("reverse", (sp, client) =>
                {
                    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppOptions>>().Value.ReverseTranslation;
                    client.BaseAddress = new Uri(opts.LibreTranslateUrl);
                    client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
                });

                services.AddHttpClient("github", (sp, client) =>
                {
                    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<AppOptions>>().CurrentValue.GitHubDocs;
                    client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
                    client.DefaultRequestHeaders.Add("User-Agent", "InterviewTranslator/1.0");
                });

                services.AddSingleton<GitHubDocsService>();

                services.AddOpenAIAssistant();

                services.AddSingleton<ITtsService, WindowsTtsService>();
                services.AddSingleton<ConnectionStatusService>();
                services.AddSingleton<ModelWarmupService>();
                services.AddSingleton<LiveTranslationOrchestrator>();
                services.AddSingleton<BidirectionalOrchestrator>();
                services.AddSingleton<OverlayWindow>();
                services.AddSingleton<SuggestionOverlayWindow>();
                services.AddSingleton<AssistantOverlayWindow>();
                services.AddTransient<SettingsWindow>();
                services.AddTransient<AnswerCardsWindow>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        AppOptionsAccessor.Initialize(
            _host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppOptions>>());

        // AssistantOverlayWindow: orchestrator event'ini dinle, başlangıçta gizli
        var assistantOverlay = _host.Services.GetRequiredService<AssistantOverlayWindow>();
        var orchestrator = _host.Services.GetRequiredService<LiveTranslationOrchestrator>();
        orchestrator.AssistantResultPublished += result => assistantOverlay.ShowResult(result);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

