# Mimari
_Son güncelleme: 2026-05-24 (FAZ 5/6/7 tamamlandı — V2 OpenAI planı eklendi)_

## Çift Yönlü Pipeline

### EN → TR (Sistem Sesi)
```
WASAPI Loopback (IEEFloat stereo NkHz)
  ↓  WaveToSampleProvider → StereoToMonoSampleProvider
  ↓  WdlResamplingSampleProvider (→ 16kHz)
  ↓  SampleToWaveProvider16 (→ int16 mono)
  ↓  EnergyBasedVadService       (live VadOptions)
SpeechSegment
  ↓  WhisperCppSttEngine          (persistent processor, ResetAsync)
TranscriptionResult
  ↓  LibreTranslateService        (GlossaryService + ContextBuffer)
TranslationResult
  ↓  SubtitlePublisher (AddOrUpdate)
SubtitleItem → SubtitleBuffer → OverlayWindow
```

### TR → EN (Mikrofon)
```
WaveInEvent (16kHz PCM doğrudan ya da 44100Hz + resample)
  ↓  EnergyBasedVadService       (live VadOptions)
SpeechSegment
  ↓  MicWhisperSttEngine.TranscribeToTurkishAsync   (processor 1)
  ↓  MicWhisperSttEngine.TranslateToEnglishAsync    (processor 2, paralel)
TR metin + EN öneri
  ↓  SuggestionPublisher
SuggestionItem → SuggestionBuffer → SuggestionOverlayWindow
```

## Projeler

| Proje | Sorumluluk |
|-------|-----------|
| Shared | DTO'lar, interface kontratlar, AppOptions (tüm alt seçenekler) |
| Audio | WASAPI Loopback + MicWaveIn; pure-NAudio resampling pipeline |
| Vad | Energy-based VAD; tüm eşikler `VadOptions`'tan canlı okunur |
| Stt | WhisperCppSttEngine (EN), MicWhisperSttEngine (TR+translate); persistent processor + ResetAsync |
| Translate | LibreTranslate HTTP, GlossaryService (130+ terim), TranslationContextBuffer |
| Subtitles | SubtitleBuffer/Publisher, SuggestionBuffer/Publisher |
| Desktop | WPF UI, Orchestratorlar, DI/Host, Serilog, GlobalHotkeyService, SettingsWindow |

## Options Hiyerarşisi (AppOptions)

```
AppOptions
├── AudioOptions          (PreferredDeviceId, SampleRate)
├── AudioProcessingOptions (LoopbackGain [canlı], ResamplerQuality, ChunkMs)
├── MicrophoneOptions     (PreferredDeviceId)
├── VadOptions            (SpeechThreshold, SilenceMs, MinSpeechMs, MaxSpeechMs — tümü canlı)
├── SttOptions [Stt]      (ModelPath, Language, ModelType, NoSpeechThreshold, Temperature)
├── SttOptions [MicStt]   (ModelPath, Language="tr", ModelType, NoSpeechThreshold, Temperature)
├── TranslationOptions    (LibreTranslateUrl, SourceLanguage, TargetLanguage, TimeoutSeconds)
├── TranslationOptions    [ReverseTranslation] (TR→EN)
├── OverlayOptions        (FontSize, Opacity, ShowEnglish, MaxLines)
├── TtsOptions            (Enabled, Rate, Volume — appsettings'e kalıcı)
└── PrivacyOptions        (SaveAudio, SaveTranscript, DebugLog)
```

Tüm seçenekler `appsettings.json`'a kalıcı olarak yazılır (`SettingsWindow → Kaydet`).

## Audio Pipeline Detayı

WASAPI Loopback `IEEFloat` (32-bit float stereo, genellikle 48kHz) döndürür.
`MediaFoundationResampler` bu formatta sessiz çıktı üretebilir; bu nedenle saf NAudio pipeline kullanılır:

```
BufferedWaveProvider (inFmt: IEEFloat stereo NkHz)
  → WaveToSampleProvider          (float samples)
  → StereoToMonoSampleProvider    (kanalları ortala)
  → WdlResamplingSampleProvider   (16kHz)
  → SampleToWaveProvider16        (int16 PCM)
```

Gain (`AudioProcessingOptions.LoopbackGain`) ham int16 baytlara canlı uygulanır;
yeniden başlatmak gerekmez.

## Queue Modeli

Her katman `System.Threading.Channels` ile async producer/consumer pattern kullanır.

| Kanal | Tip | FullMode |
|-------|-----|---------|
| Loopback audio | `Channel<AudioFrame>` bounded 400 | DropOldest |
| Mic audio | `Channel<AudioFrame>` bounded 400 | DropOldest |
| Subtitle | `SubtitleBuffer` (ConcurrentDictionary) | AddOrUpdate |
| Suggestion | `SuggestionBuffer` (ConcurrentDictionary) | AddOrUpdate |

VAD ve STT arası: `IAsyncEnumerable<SpeechSegment>` — doğrudan streaming, buffer yok.

## Hotkey Sistemi

`GlobalHotkeyService` — Win32 `SetWindowsHookEx(WH_KEYBOARD_LL)` ile uygulama arka planda iken de çalışır.

| Tuş | Aksiyon |
|-----|---------|
| F1  | EN→TR sistem sesi başlat |
| F2  | EN→TR sistem sesi durdur |
| F3  | EN→TR overlay gizle / göster |
| F4  | Cevap Kartları paneli aç |
| F5  | Ayarlar penceresi aç |
| F6  | TR→EN öneri overlay gizle / göster |

## WPF Pencereler

| Pencere | Açıklama | Hotkey |
|---------|----------|--------|
| MainWindow | Kontrol paneli — cihaz seçimi, başlat/durdur, log | — |
| OverlayWindow | Always-on-top saydam altyazı; EN beyaz + TR açık mavi (TextBlock.Inlines) | F3 |
| SuggestionOverlayWindow | Sol üst köşe; TR mor + EN beyaz (TextBlock.Inlines) | F6 |
| SettingsWindow | 6 sekme: Görünüm / Ses / VAD / Whisper / Çeviri / TTS | F5 |
| AnswerCardsWindow | 28 hazır cevap kartı, canlı arama, kopyalama | F4 |

### SettingsWindow Sekmeleri

| Sekme | Kontroller | Etki |
|-------|-----------|------|
| Görünüm | FontSize, Opacity, ShowEnglish, CompactMode | Kaydet sonrası |
| Ses | LoopbackGain (canlı), ResamplerQuality | Gain anında; Kalite başlatmada |
| VAD | SpeechThreshold, SilenceMs, MinSpeechMs, MaxSpeechMs; 3 önayar | Anında (canlı) |
| Whisper | EN modeli, TR modeli, NoSpeechThreshold, Temperature; Sıfırla butonu | Sıfırla sonrası |
| Çeviri | LibreTranslateUrl, TimeoutSeconds | Kaydet sonrası |
| TTS | Enabled, Rate, Volume | Kaydet sonrası |

## Model Warmup

`ModelWarmupService` uygulama açılışında her iki Whisper modelini (EN + TR) arka planda doğrular/indirir.
Tamamlanana kadar **Başlat** ve **Mic Başlat** butonları devre dışıdır.

## Translation Pipeline (FAZ 3)

```
TranscriptionResult.Text
  ↓  TranslationContextBuffer.IsDuplicate()  → %90+ Jaccard → atla
  ↓  TranslationContextBuffer.BuildContextualText()  → son 3 segment prefix
  ↓  GlossaryService.Protect()  → 130+ teknik terim placeholder
  ↓  LibreTranslate HTTP POST /translate
  ↓  GlossaryService.Restore()
  ↓  ExtractLastSentence()  → bağlam prefixini soy
TranslationResult.TranslatedText
```

## Test Kapsamı

| Test Sınıfı | Test Sayısı | Kapsam |
|-------------|------------|--------|
| GlossaryServiceTests | 7 | Terim koruma, geri yükleme, case-insensitive |
| SubtitleBufferTests | 6 | Kapasite, thread-safety, event |
| EnergyVadTests | 4 | Sessizlik, tek/çift segment, sample rate |
| SuggestionBufferTests | 7 | Kapasite, thread-safety, AddOrUpdate |

## Gizlilik Defaults

| Ayar | Varsayılan |
|------|-----------|
| SaveAudio | false |
| SaveTranscript | false |
| DebugLog | false |
| CloudAPI | YOK — tüm işlem lokal |

---

# V2 — OpenAI Entegrasyonlu Mimari (Planlanan)

> Kaynak: `canli_ceviri_uygulamasi_chatgpt_openai_entegrasyonlu_teknik_analiz.pdf` (2026-05-24)

## V2 Pipeline (Revize)

```
Audio Capture + VAD
  ↓
STT Router
  ├─ Local: whisper.cpp (mevcut)
  └─ OpenAI: Realtime Transcription (V2-B)
  ↓
Translation Router
  ├─ Local: LibreTranslate (mevcut)
  └─ OpenAI: Realtime Translate / GPT (V2-C)
  ↓
Quality Gate
  ├─ Confidence score
  ├─ Technical glossary
  ├─ Hallucination / low-confidence uyarısı
  └─ Orijinal EN transcript her zaman görünür
  ↓
Assistant Layer                          ← V2-A ile eklenir
  ├─ Kısa Türkçe özet
  ├─ Soru niyeti tespiti
  ├─ Cevap kartı önerisi
  └─ Clarification cümlesi önerisi
  ↓
Overlay UI
```

## V2 Proje Yapısı (Planlanan)

| Proje | Sorumluluk |
|-------|-----------|
| OpenAIConnector | OpenAI API entegrasyonu — UI/audio'ya bağımlılık yok |
| Shared (güncelleme) | Yeni interface kontratlar + V2 DTO'ları |

## V2 Interface Kontratları

```csharp
public interface IRealtimeTranscriptionProvider
{
    IAsyncEnumerable<TranscriptDelta> TranscribeAsync(
        IAsyncEnumerable<AudioFrame> audioFrames,
        CancellationToken cancellationToken);
}

public interface ITranslationProvider
{
    Task<TranslationResult> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken);
}

public interface IInterviewAssistantProvider
{
    Task<InterviewAssistResult> AnalyzeQuestionAsync(
        InterviewAssistRequest request,
        CancellationToken cancellationToken);
}

public interface IAssistantSummaryProvider { }
public interface IAnswerSuggestionProvider { }
public interface IGlossaryCorrectionProvider { }
public interface IQualityEvaluator { }
```

## V2 DTO'ları

```csharp
public sealed class InterviewAssistResult
{
    public string TurkishSummary     { get; init; } = "";
    public string DetectedIntent     { get; init; } = "";  // teknik|davranışsal|deneyim|maaş
    public string SuggestedAnswerKey { get; init; } = "";
    public double Confidence         { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
```

## V2 AppOptions Eklentileri (Planlanan)

```
AppOptions
└── OpenAIOptions
    ├── ApiKey              (kullanıcı girişi, appsettings'e şifreli)
    ├── Mode                (Disabled | AssistantOnly | HybridSTT | FullCloud)
    ├── SttModel            (whisper-1 | gpt-4o-realtime)
    ├── TranslationModel    (gpt-4o | gpt-4o-mini)
    ├── AssistantModel      (gpt-4o-mini — özet/intent için)
    ├── MaxTokensPerRequest (kota kontrolü)
    └── ShowCloudWarning    (true — bulut API uyarısı)
```

## V2 SettingsWindow (Planlanan)

Mevcut 6 sekmeye 7. sekme eklenir:

| Sekme | Kontroller |
|-------|-----------|
| OpenAI | Mod seçimi, API Key, model seçimi, kota limiti, bulut uyarısı toggle |

## Mod Geçiş Mantığı

```
OpenAIOptions.Mode = Disabled
  → Mevcut V1 pipeline (lokal, ücretsiz)

OpenAIOptions.Mode = AssistantOnly        ← V2-A (ilk adım)
  → STT + çeviri lokal
  → TranslationResult → IInterviewAssistantProvider → AssistantOverlay

OpenAIOptions.Mode = HybridSTT            ← V2-B
  → STT = IRealtimeTranscriptionProvider (OpenAI)
  → Çeviri lokal

OpenAIOptions.Mode = FullCloud            ← V2-C (Pro)
  → STT + çeviri + assistant → OpenAI
```

## V2 Gizlilik Kararları

| Durum | Davranış |
|-------|---------|
| Local Free Mode | Ses ve transcript cihazı terk etmez |
| OpenAI modu açılışta | "Ses/transcript bulut API'ye gönderilir" uyarısı zorunlu |
| API Key | appsettings'e yazılır, log'a asla basılmaz |
| Ham ses | Varsayılan kayıt kapalı (V1 ile aynı) |
