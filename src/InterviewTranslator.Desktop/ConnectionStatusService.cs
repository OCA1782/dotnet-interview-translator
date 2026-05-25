using System.Net.Http;
using InterviewTranslator.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewTranslator.Desktop;

public sealed class ConnectionStatusService : IDisposable
{
    public bool IsConnected { get; private set; }
    public event Action<bool>? StatusChanged;

    private readonly HttpClient _http;
    private readonly ILogger<ConnectionStatusService> _logger;
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(5));
    private Task? _loop;
    private CancellationTokenSource? _cts;

    public ConnectionStatusService(IOptions<AppOptions> options, ILogger<ConnectionStatusService> logger)
    {
        _logger = logger;
        _http = new HttpClient
        {
            BaseAddress = new Uri(options.Value.Translation.LibreTranslateUrl),
            Timeout = TimeSpan.FromSeconds(3)
        };
    }

    public void Start()
    {
        _cts  = new CancellationTokenSource();
        _loop = RunAsync(_cts.Token);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        // İlk kontrol hemen
        await CheckOnceAsync();

        try
        {
            while (await _timer.WaitForNextTickAsync(ct))
                await CheckOnceAsync();
        }
        catch (OperationCanceledException) { }
    }

    private async Task CheckOnceAsync()
    {
        try
        {
            var resp = await _http.GetAsync("/languages");
            SetStatus(resp.IsSuccessStatusCode);
        }
        catch
        {
            SetStatus(false);
        }
    }

    private void SetStatus(bool connected)
    {
        if (connected == IsConnected) return;
        IsConnected = connected;
        _logger.LogInformation("LibreTranslate bağlantısı: {Status}", connected ? "OK" : "KESİLDİ");
        StatusChanged?.Invoke(connected);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _timer.Dispose();
        _http.Dispose();
    }
}
