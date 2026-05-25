using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Shared.Options;
using InterviewTranslator.Stt;
using Microsoft.Extensions.Options;

namespace InterviewTranslator.Desktop;

public partial class SettingsWindow : Window
{
    private readonly OverlayWindow          _overlay;
    private readonly WindowsTtsService      _tts;
    private readonly WhisperCppSttEngine    _sttEn;
    private readonly MicWhisperSttEngine    _sttTr;

    private readonly OverlayOptions         _overlayOpts;
    private readonly AudioProcessingOptions _audioOpts;
    private readonly VadOptions             _vadOpts;
    private readonly SttOptions             _sttEnOpts;
    private readonly SttOptions             _sttTrOpts;
    private readonly TranslationOptions     _transOpts;
    private readonly TtsOptions             _ttsOpts;

    public SettingsWindow(
        IOptions<AppOptions> options,
        OverlayWindow overlay,
        ITtsService tts,
        WhisperCppSttEngine sttEn,
        MicWhisperSttEngine sttTr)
    {
        // Alan atamalarını InitializeComponent'tan ÖNCE yap —
        // aksi hâlde XAML slider'ları yüklenirken ValueChanged olayları
        // henüz null olan alanlara erişir ve NullReferenceException fırlatır.
        var o        = options.Value;
        _overlayOpts = o.Overlay;
        _audioOpts   = o.AudioProcessing;
        _vadOpts     = o.Vad;
        _sttEnOpts   = o.Stt;
        _sttTrOpts   = o.MicStt;
        _transOpts   = o.Translation;
        _ttsOpts     = o.Tts;
        _overlay     = overlay;
        _tts         = (WindowsTtsService)tts;
        _sttEn       = sttEn;
        _sttTr       = sttTr;

        InitializeComponent();
        LoadValues();
    }

    private void LoadValues()
    {
        // ── Görünüm ──
        FontSizeSlider.Value      = _overlayOpts.FontSize;
        OpacitySlider.Value       = _overlayOpts.Opacity;
        ShowEnglishCheck.IsChecked  = _overlayOpts.ShowEnglish;
        CompactModeCheck.IsChecked  = _overlayOpts.MaxLines == 1;

        // ── Ses ──
        GainSlider.Value          = _audioOpts.LoopbackGain;
        SelectComboByTag(ResamplerQualityCombo, _audioOpts.ResamplerQuality.ToString());

        // ── VAD ──
        EnergySlider.Value        = _vadOpts.SpeechThreshold;
        SilenceSlider.Value       = _vadOpts.SilenceMs;
        MinSpeechSlider.Value     = _vadOpts.MinSpeechMs;
        MaxSpeechSlider.Value     = _vadOpts.MaxSpeechMs;

        // ── Whisper ──
        SelectComboByTag(EnModelCombo,  _sttEnOpts.ModelType);
        SelectComboByTag(TrModelCombo,  _sttTrOpts.ModelType);
        NoSpeechSlider.Value      = _sttEnOpts.NoSpeechThreshold;
        SelectComboByTag(TempCombo,     _sttEnOpts.Temperature.ToString("F1"));

        // ── Çeviri ──
        LibreUrlBox.Text          = _transOpts.LibreTranslateUrl;
        TimeoutSlider.Value       = _transOpts.TimeoutSeconds;

        // ── TTS ──
        TtsEnabledCheck.IsChecked = _ttsOpts.Enabled;
        TtsRateSlider.Value       = _ttsOpts.Rate;
        TtsVolumeSlider.Value     = _ttsOpts.Volume;
        TtsRateSlider.IsEnabled   = _ttsOpts.Enabled;
        TtsVolumeSlider.IsEnabled = _ttsOpts.Enabled;
    }

    // ──────────────── Görünüm handlers ────────────────

    private void FontSizeSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    { if (FontSizeLabel is not null) FontSizeLabel.Text = $"{(int)e.NewValue}px"; }

    private void OpacitySlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    { if (OpacityLabel is not null) OpacityLabel.Text = $"{e.NewValue:P0}"; }

    private void ResetPositionBtn_Click(object s, RoutedEventArgs e) => _overlay.ResetPosition();

    // ──────────────── Ses handlers ────────────────

    private void GainSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (GainLabel is null) return;
        GainLabel.Text        = $"{e.NewValue:F1}x";
        _audioOpts.LoopbackGain = (float)e.NewValue; // anında geçerli
    }

    // ──────────────── VAD handlers ────────────────

    private void EnergySlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (EnergyLabel is not null) EnergyLabel.Text = $"{e.NewValue:F3}";
        if (_vadOpts is not null) _vadOpts.SpeechThreshold = e.NewValue;
    }

    private void SilenceSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SilenceLabel is not null) SilenceLabel.Text = $"{(int)e.NewValue}ms";
        if (_vadOpts is not null) _vadOpts.SilenceMs = (int)e.NewValue;
    }

    private void MinSpeechSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MinSpeechLabel is not null) MinSpeechLabel.Text = $"{(int)e.NewValue}ms";
        if (_vadOpts is not null) _vadOpts.MinSpeechMs = (int)e.NewValue;
    }

    private void MaxSpeechSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MaxSpeechLabel is not null) MaxSpeechLabel.Text = $"{(int)e.NewValue}ms";
        if (_vadOpts is not null) _vadOpts.MaxSpeechMs = (int)e.NewValue;
    }

    // ──────────────── VAD önayarlar ────────────────

    private void PresetSensitive_Click(object s, RoutedEventArgs e)
    {
        EnergySlider.Value    = 0.001;
        SilenceSlider.Value   = 200;
        MinSpeechSlider.Value = 80;
        MaxSpeechSlider.Value = 3000;
        ShowStatus("✓ Duyarlı önayarı uygulandı");
    }

    private void PresetBalanced_Click(object s, RoutedEventArgs e)
    {
        EnergySlider.Value    = 0.003;
        SilenceSlider.Value   = 240;
        MinSpeechSlider.Value = 120;
        MaxSpeechSlider.Value = 2000;
        ShowStatus("✓ Dengeli önayarı uygulandı");
    }

    private void PresetSelective_Click(object s, RoutedEventArgs e)
    {
        EnergySlider.Value    = 0.008;
        SilenceSlider.Value   = 400;
        MinSpeechSlider.Value = 200;
        MaxSpeechSlider.Value = 2500;
        ShowStatus("✓ Seçici önayarı uygulandı");
    }

    // ──────────────── Whisper handlers ────────────────

    private void NoSpeechSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    { if (NoSpeechLabel is not null) NoSpeechLabel.Text = $"{e.NewValue:F2}"; }

    private async void ResetSttBtn_Click(object s, RoutedEventArgs e)
    {
        ResetSttBtn.IsEnabled = false;
        ResetSttBtn.Content   = "Sıfırlanıyor...";
        ApplyWhisperOptions();
        await Task.WhenAll(_sttEn.ResetAsync(), _sttTr.ResetAsync());
        ResetSttBtn.Content   = "Whisper Processor'ı Sıfırla";
        ResetSttBtn.IsEnabled = true;
        ShowStatus("✓ Whisper sıfırlandı — bir sonraki konuşmada yeni model yüklenir");
    }

    // ──────────────── Çeviri handlers ────────────────

    private void TimeoutSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    { if (TimeoutLabel is not null) TimeoutLabel.Text = $"{(int)e.NewValue}s"; }

    // ──────────────── TTS handlers ────────────────

    private void TtsRateSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    { if (TtsRateLabel is not null) TtsRateLabel.Text = ((int)e.NewValue).ToString("+0;-0;0"); }

    private void TtsVolumeSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    { if (TtsVolumeLabel is not null) TtsVolumeLabel.Text = $"{(int)e.NewValue}%"; }

    private void TtsCheck_Changed(object s, RoutedEventArgs e)
    {
        if (TtsRateSlider is null) return;
        bool en = TtsEnabledCheck.IsChecked == true;
        TtsRateSlider.IsEnabled   = en;
        TtsVolumeSlider.IsEnabled = en;
    }

    // ──────────────── Save / Cancel ────────────────

    private void SaveBtn_Click(object s, RoutedEventArgs e)
    {
        // Görünüm
        _overlayOpts.FontSize    = FontSizeSlider.Value;
        _overlayOpts.Opacity     = OpacitySlider.Value;
        _overlayOpts.ShowEnglish = ShowEnglishCheck.IsChecked == true;
        _overlayOpts.MaxLines    = CompactModeCheck.IsChecked == true ? 1 : 3;

        // Ses (Gain zaten canlı güncellendi)
        _audioOpts.ResamplerQuality = int.Parse(
            (ResamplerQualityCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "1");

        // VAD (slider event'ler zaten güncelledi — sadece emin olalım)
        _vadOpts.SpeechThreshold = EnergySlider.Value;
        _vadOpts.SilenceMs       = (int)SilenceSlider.Value;
        _vadOpts.MinSpeechMs     = (int)MinSpeechSlider.Value;
        _vadOpts.MaxSpeechMs     = (int)MaxSpeechSlider.Value;

        // Whisper
        ApplyWhisperOptions();

        // Çeviri
        _transOpts.LibreTranslateUrl = LibreUrlBox.Text.Trim();
        _transOpts.TimeoutSeconds    = (int)TimeoutSlider.Value;

        // TTS
        _ttsOpts.Enabled = TtsEnabledCheck.IsChecked == true;
        _ttsOpts.Rate    = (int)TtsRateSlider.Value;
        _ttsOpts.Volume  = (int)TtsVolumeSlider.Value;
        _tts.ApplyOptions();

        _overlay.ApplyCurrentOptions();
        PersistToAppSettings();
        Close();
    }

    private void CancelBtn_Click(object s, RoutedEventArgs e) => Close();

    // ──────────────── Helpers ────────────────

    private void ApplyWhisperOptions()
    {
        var enModel = (EnModelCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "tiny.en";
        var trModel = (TrModelCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "tiny";
        var temp    = float.Parse((TempCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "0.0",
                          System.Globalization.CultureInfo.InvariantCulture);

        _sttEnOpts.ModelType          = enModel;
        _sttEnOpts.ModelPath          = $"workers/models/ggml-{enModel}.bin";
        _sttEnOpts.NoSpeechThreshold  = (float)NoSpeechSlider.Value;
        _sttEnOpts.Temperature        = temp;

        _sttTrOpts.ModelType          = trModel;
        _sttTrOpts.ModelPath          = $"workers/models/ggml-{trModel}.bin";
        _sttTrOpts.NoSpeechThreshold  = (float)NoSpeechSlider.Value;
        _sttTrOpts.Temperature        = temp;
    }

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (ComboBoxItem item in combo.Items)
            if (item.Tag?.ToString() == tag) { combo.SelectedItem = item; return; }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private void ShowStatus(string msg)
    {
        StatusMsg.Text = msg;
        // 3sn sonra temizle
        var timer = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) => { StatusMsg.Text = ""; timer.Stop(); };
        timer.Start();
    }

    private void PersistToAppSettings()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path)) return;

        try
        {
            var json    = JsonNode.Parse(File.ReadAllText(path))!;
            var app     = json["App"];
            if (app is null) return;

            var opts = AppOptionsAccessor.Current;

            Set(app, "Overlay",          n =>
            {
                n["FontSize"]    = opts.Overlay.FontSize;
                n["Opacity"]     = opts.Overlay.Opacity;
                n["ShowEnglish"] = opts.Overlay.ShowEnglish;
                n["MaxLines"]    = opts.Overlay.MaxLines;
            });

            Set(app, "AudioProcessing",  n =>
            {
                n["LoopbackGain"]     = opts.AudioProcessing.LoopbackGain;
                n["ResamplerQuality"] = opts.AudioProcessing.ResamplerQuality;
                n["ChunkMs"]          = opts.AudioProcessing.ChunkMs;
            });

            Set(app, "Vad",              n =>
            {
                n["SpeechThreshold"] = opts.Vad.SpeechThreshold;
                n["SilenceMs"]       = opts.Vad.SilenceMs;
                n["MinSpeechMs"]     = opts.Vad.MinSpeechMs;
                n["MaxSpeechMs"]     = opts.Vad.MaxSpeechMs;
            });

            Set(app, "Stt",              n =>
            {
                n["ModelPath"]          = opts.Stt.ModelPath;
                n["ModelType"]          = opts.Stt.ModelType;
                n["NoSpeechThreshold"]  = opts.Stt.NoSpeechThreshold;
                n["Temperature"]        = opts.Stt.Temperature;
            });

            Set(app, "MicStt",           n =>
            {
                n["ModelPath"]          = opts.MicStt.ModelPath;
                n["ModelType"]          = opts.MicStt.ModelType;
                n["NoSpeechThreshold"]  = opts.MicStt.NoSpeechThreshold;
                n["Temperature"]        = opts.MicStt.Temperature;
            });

            Set(app, "Translation",      n =>
            {
                n["LibreTranslateUrl"] = opts.Translation.LibreTranslateUrl;
                n["TimeoutSeconds"]    = opts.Translation.TimeoutSeconds;
            });

            Set(app, "Tts",              n =>
            {
                n["Enabled"] = opts.Tts.Enabled;
                n["Rate"]    = opts.Tts.Rate;
                n["Volume"]  = opts.Tts.Volume;
            });

            File.WriteAllText(path,
                json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* IO hatası sessizce geç */ }
    }

    private static void Set(JsonNode app, string key, Action<JsonNode> setter)
    {
        var node = app[key];
        if (node is not null) setter(node);
    }
}
