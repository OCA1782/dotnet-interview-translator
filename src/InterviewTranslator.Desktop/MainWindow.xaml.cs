using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using InterviewTranslator.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InterviewTranslator.Desktop;

public partial class MainWindow : Window
{
    private readonly LiveTranslationOrchestrator _orchestrator;
    private readonly BidirectionalOrchestrator _bidirectional;
    private readonly IAudioCaptureService _audioCapture;
    private readonly IMicrophoneCaptureService _micCapture;
    private readonly OverlayWindow _overlay;
    private readonly SuggestionOverlayWindow _suggestionOverlay;
    private readonly AssistantOverlayWindow _assistantOverlay;
    private readonly ConnectionStatusService _connStatus;
    private readonly ModelWarmupService _warmup;
    private readonly IServiceProvider _services;
    private readonly GlobalHotkeyService _hotkey = new();
    private readonly ILogger<MainWindow> _logger;

    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _micCts;
    private AnswerCardsWindow? _cardsWindow;

    public MainWindow(
        LiveTranslationOrchestrator orchestrator,
        BidirectionalOrchestrator bidirectional,
        IAudioCaptureService audioCapture,
        IMicrophoneCaptureService micCapture,
        OverlayWindow overlay,
        SuggestionOverlayWindow suggestionOverlay,
        AssistantOverlayWindow assistantOverlay,
        ConnectionStatusService connStatus,
        ModelWarmupService warmup,
        IServiceProvider services,
        ILogger<MainWindow> logger)
    {
        InitializeComponent();
        _logger            = logger;
        _orchestrator      = orchestrator;
        _bidirectional     = bidirectional;
        _audioCapture      = audioCapture;
        _micCapture        = micCapture;
        _overlay           = overlay;
        _suggestionOverlay = suggestionOverlay;
        _assistantOverlay  = assistantOverlay;
        _connStatus        = connStatus;
        _warmup            = warmup;
        _services          = services;

        _connStatus.StatusChanged += OnConnectionStatusChanged;
        _connStatus.Start();

        StartBtn.IsEnabled    = false;
        MicStartBtn.IsEnabled = false;

        _warmup.StatusChanged += msg =>
        {
            Log($"[Model] {msg}");
            if (msg.Contains("hazır ✓") && msg.Contains("Modeller"))
                Dispatcher.Invoke(() => { StartBtn.IsEnabled = true; MicStartBtn.IsEnabled = true; });
        };
        _warmup.StartAsync();

        RegisterHotkeys();
        LoadDevices();

        _overlay.Show();
        _suggestionOverlay.Show();
        Application.Current.MainWindow = this;
    }

    private void OnConnectionStatusChanged(bool connected)
    {
        Dispatcher.Invoke(() =>
        {
            StatusDot.Fill   = connected
                ? new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32))  // yeşil
                : new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));  // kırmızı
            StatusLabel.Text = connected ? "LibreTranslate ✓" : "LibreTranslate ✗";
        });
    }

    private void LoadDevices()
    {
        var audioDevices = _audioCapture.GetOutputDevices();
        DeviceCombo.ItemsSource = audioDevices;
        DeviceCombo.DisplayMemberPath = "Name";
        DeviceCombo.SelectedValuePath = "Id";
        if (audioDevices.Count > 0)
            DeviceCombo.SelectedIndex = 0;

        var micDevices = _micCapture.GetInputDevices();
        MicCombo.ItemsSource = micDevices;
        MicCombo.DisplayMemberPath = "Name";
        MicCombo.SelectedValuePath = "Id";
        if (micDevices.Count > 0)
            MicCombo.SelectedIndex = 0;
    }

    private void DeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DeviceCombo.SelectedValue is string id)
            _audioCapture.SetDevice(id);
    }

    private void MicCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MicCombo.SelectedValue is string id)
            _micCapture.SetDevice(id);
    }

    private async void StartBtn_Click(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        StartBtn.IsEnabled = false;
        StopBtn.IsEnabled = true;
        Log("Sistem sesi dinleniyor (EN→TR)...");

        try
        {
            await _orchestrator.StartAsync(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            Log("Durduruldu.");
        }
        catch (Exception ex)
        {
            Log($"Hata: {ex.Message}");
        }
        finally
        {
            StartBtn.IsEnabled = true;
            StopBtn.IsEnabled = false;
        }
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private async void MicStartBtn_Click(object sender, RoutedEventArgs e)
    {
        _micCts = new CancellationTokenSource();
        MicStartBtn.IsEnabled = false;
        MicStopBtn.IsEnabled = true;
        Log("Mikrofon dinleniyor (TR→EN)...");

        try
        {
            await _bidirectional.StartAsync(_micCts.Token);
        }
        catch (OperationCanceledException)
        {
            Log("Mikrofon durduruldu.");
        }
        catch (Exception ex)
        {
            Log($"Mikrofon hatası: {ex.Message}");
        }
        finally
        {
            MicStartBtn.IsEnabled = true;
            MicStopBtn.IsEnabled = false;
        }
    }

    private void MicStopBtn_Click(object sender, RoutedEventArgs e) => _micCts?.Cancel();

    private void OverlayBtn_Click(object sender, RoutedEventArgs e) => _overlay.ToggleVisibility();

    private void CardsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_cardsWindow is null || !_cardsWindow.IsLoaded)
        {
            _cardsWindow = _services.GetRequiredService<AnswerCardsWindow>();
            _cardsWindow.Show();
        }
        else
        {
            _cardsWindow.Activate();
        }
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var win = _services.GetRequiredService<SettingsWindow>();
            win.Owner = this;
            win.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Settings] Ayarlar penceresi açılamadı");
            var msg = ex.InnerException?.Message ?? ex.Message;
            Log($"[Settings HATA] {msg}");
            System.Windows.MessageBox.Show(ex.ToString(), "Ayarlar Hatası",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void RegisterHotkeys()
    {
        _hotkey.Register(Key.F1, () => StartBtn_Click(this, new RoutedEventArgs()));
        _hotkey.Register(Key.F2, () => StopBtn_Click(this, new RoutedEventArgs()));
        _hotkey.Register(Key.F3, () => OverlayBtn_Click(this, new RoutedEventArgs()));
        _hotkey.Register(Key.F4, () => CardsBtn_Click(this, new RoutedEventArgs()));
        _hotkey.Register(Key.F5, () => SettingsBtn_Click(this, new RoutedEventArgs()));
        _hotkey.Register(Key.F6, () => _suggestionOverlay.ToggleVisibility());
        _hotkey.Register(Key.F7, () => _assistantOverlay.ToggleVisibility());
        _hotkey.Start();
    }

    private void Log(string message)
    {
        Dispatcher.Invoke(() =>
        {
            StatusLog.Text += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts?.Cancel();
        _micCts?.Cancel();
        _connStatus.Dispose();
        _hotkey.Dispose();
        _overlay.Close();
        _suggestionOverlay.Close();
        _assistantOverlay.Close();
        base.OnClosed(e);
    }
}
