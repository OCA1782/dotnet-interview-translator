# MVP Planı

## FAZ 0 — Teknik Doğrulama [✓ tamamlandı]
- ✅ Console app projesi (InterviewTranslator.ConsoleTest)
- ✅ Tam pipeline testi: ses → VAD → STT → LibreTranslate → konsol
- ✅ LibreTranslate Docker entegrasyonu (`scripts/setup-libretranslate.ps1`)
- ✅ Whisper model indirme (`scripts/setup-whisper.ps1`)

## FAZ 1 — MVP Pipeline [✓ tamamlandı]
- ✅ Audio capture + VAD + STT worker + Translation worker
- ✅ WPF MainWindow + OverlayWindow (always-on-top, saydam)
- ✅ Start/Stop + device selector
- ✅ appsettings.json konfigürasyon
- ✅ Serilog (console + file sink)
- ✅ DI/Host (Microsoft.Extensions.Hosting)

## FAZ 2 — Overlay UX [✓ tamamlandı]
- ✅ Ekran pozisyonu dinamik hesaplama (alt %12, yatay orta)
- ✅ Global hotkey — Win32 low-level hook (F1-F6)
- ✅ SettingsWindow — 6 sekme, tüm parametreler
- ✅ Compact mod (MaxLines=1 → overlay yüksekliği küçülür)

## FAZ 3 — Doğruluk İyileştirme [✓ tamamlandı]
- ✅ GlossaryService: 130+ teknik terim, 9 kategori
- ✅ TranslationContextBuffer: son 3 segment bağlamı, Jaccard duplicate filtre
- ✅ LibreTranslateService entegrasyonu
- ✅ VAD threshold appsettings + live-read (yeniden başlatma gerektirmez)

## FAZ 4 — Hazır Cevap Kartları [✓ tamamlandı]
- ✅ 28 kart: Teknik (15), Behavioral (7), Genel (6)
- ✅ Canlı arama + kategori sekmeleri
- ✅ Önizleme + tek tıkla kopyalama
- ✅ F4 ile aç/kapat, ekran sağ üst köşe, Topmost

## FAZ 5 — İki Yönlü Çeviri [✓ tamamlandı]
- ✅ IMicrophoneCaptureService + MicrophoneCaptureService (16kHz doğrudan veya 44100Hz fallback)
- ✅ MicWhisperSttEngine — iki bağımsız persistent processor (TR transkript + TR→EN çeviri)
- ✅ SuggestionBuffer + SuggestionPublisher + ISuggestionPublisher
- ✅ BidirectionalOrchestrator — Mic → VAD → STT(TR) → LibreTranslate(TR→EN) → Publisher
- ✅ SuggestionOverlayWindow — sol üst köşe, TextBlock.Inlines (TR mor, EN beyaz), F6 toggle
- ✅ MainWindow: mikrofon combo + Mic Başlat/Durdur butonları
- ✅ WindowsTtsService: EN önerisini sesli okur (System.Speech)

## FAZ 6 — Kalite ve UX [✓ tamamlandı]
- ✅ SettingsWindow 6 sekme: Görünüm / Ses / VAD / Whisper / Çeviri / TTS
- ✅ AudioProcessingOptions: LoopbackGain (canlı), ResamplerQuality, ChunkMs
- ✅ VadOptions canlı okuma — yeniden başlatma gerekmez
- ✅ SttOptions: ModelType, NoSpeechThreshold, Temperature; ResetAsync() butonu
- ✅ TTS: Enabled / Rate / Volume slider; appsettings'e kalıcı yazma
- ✅ ConnectionStatusService: 5sn health-check, yeşil/kırmızı indicator
- ✅ ModelWarmupService: açılışta her iki model doğrulanır, Start butonları warmup bitene kadar pasif
- ✅ AppOptionsAccessor: DI options'a static erişim
- ✅ SuggestionBufferTests: 7 yeni test → toplam 26/26 geçiyor

## FAZ 7 — Audio Pipeline ve Kararlılık [✓ tamamlandı]
- ✅ WASAPI Loopback: MediaFoundationResampler → pure NAudio pipeline (WdlResamplingSampleProvider)
  - IEEFloat stereo NkHz → float mono → 16kHz → int16 PCM
  - MediaFoundationResampler IEEFloat girişinde sessiz çıktı üretiyordu (enerji 0.0%)
- ✅ OverlayWindow: SubtitleBuffer.AddOrUpdate ile iki aşamalı EN+TR yayın desteği
- ✅ OverlayWindow + SuggestionOverlayWindow: TextBlock.Inlines ile iki renkli metin
- ✅ SettingsWindow XAML: karmaşık ControlTemplate → standart WPF kontroller (NullRefException düzeltildi)
- ✅ MainWindow SettingsBtn try-catch + Serilog hata loglama
- ✅ MicWhisperSttEngine: iki bağımsız factory+processor (paralel çalışım)

## Başarı Kriterleri (FAZ 1)
- YouTube İngilizce video yakalanır
- 3-5 sn gecikmeyle Türkçe altyazı görünür
- Manuel metin taşıma gerektirmez
- Uygulama kapansa görüşme bozulmaz
- Varsayılan: ses/transcript kaydedilmez

## Bilinen Sınırlamalar
- LibreTranslate Docker çalışıyor olmalı (`docker start libretranslate`)
- İlk model warmup internet bağlantısı gerektirir (~74 MB × 2 model)
- WASAPI Loopback: loopback cihazın seçili ses çıkışıyla eşleşmesi gerekir

---

# V2 — OpenAI Entegrasyonlu Mimari

> Kaynak: `canli_ceviri_uygulamasi_chatgpt_openai_entegrasyonlu_teknik_analiz.pdf` (2026-05-24)
>
> V1 lokal/ücretsiz çekirdek korunur. OpenAI entegrasyonu opsiyonel kalite modu olarak eklenir.

## Mod Tasarımı

| Mod | STT | Çeviri | Maliyet |
|-----|-----|--------|---------|
| Local Free Mode | whisper.cpp | LibreTranslate | Ücretsiz |
| Hybrid Quality Mode | OpenAI Realtime veya local | OpenAI veya local | Kullanıma bağlı |
| OpenAI First Mode | GPT-Realtime-Whisper | GPT-Translate | Token/dakika bazlı |

## Entegrasyon Senaryoları

- **Senaryo A** *(ilk eklenecek mod)*: STT+çeviri lokal, OpenAI sadece anlam özeti + cevap kartı üretir
- **Senaryo B**: OpenAI Realtime STT, çeviri lokal — ses buluta gider
- **Senaryo C** *(Pro Mode)*: OpenAI uçtan uca — ürün olgunlaştıktan sonra

## Cevap Üretimi Seviyeleri

| Seviye | İçerik | Karar |
|--------|--------|-------|
| 1 | Türkçe altyazı | ✅ V1 MVP |
| 2 | Kısa özet + soru niyeti | Önerilir |
| 3 | Cevap kartı önerisi | Kontrollü önerilir |
| 4 | İngilizce cevap taslağı | Kullanıcı onayı ile |
| 5 | AI sesli cevap iletir | İş görüşmesi için önerilmez |

## FAZ-V2-A — OpenAI Anlam Katmanı [✓ tamamlandı]

- ✅ `InterviewTranslator.OpenAI` projesi — ayrı proje, UI/audio'ya bağımlılık yok
- ✅ `IInterviewAssistantProvider` — kısa Türkçe özet + soru niyeti + cevap önerisi
- ✅ `OpenAIAssistantProvider` — GPT-4o-mini HTTP, structured JSON prompt, confidence + uyarılar
- ✅ `NullAssistantProvider` — OpenAI devre dışıyken no-op implementasyon
- ✅ `OpenAIServiceExtensions.AddOpenAIAssistant()` — DI wiring, mode'a göre otomatik seçim
- ✅ `AssistantOverlayWindow` — sağ üst köşe, intent badge (renkli), Türkçe özet, confidence, uyarı
- ✅ `LiveTranslationOrchestrator.AssistantResultPublished` event — çeviri sonrası async assistant çağrısı
- ✅ F7 hotkey — AssistantOverlayWindow gizle/göster
- ✅ `OpenAIOptions` — AppOptions + appsettings.json (Mode=Disabled varsayılan)
- ✅ **SettingsWindow OpenAI sekmesi** — mod, API anahtarı, model, token, timeout, uyarı toggle
- ✅ Build: 0 hata, 0 uyarı

## FAZ-V2-B — OpenAI Realtime STT Modu [ ]

- [ ] `IRealtimeTranscriptionProvider` — `IAsyncEnumerable<TranscriptDelta>` streaming
- [ ] STT Router: local whisper.cpp ↔ OpenAI Realtime Transcription seçimi
- [ ] Kullanıcı arayüzünde "Local / OpenAI" motor toggle
- [ ] Network latency metriği overlay'de gösterimi
- [ ] Aylık/dakikalık kullanım kotası ve uyarısı
- [ ] Local STT ile karşılaştırmalı kalite ölçümü

## FAZ-V2-C — Pro Çift Yönlü + OpenAI Translate [ ]

- [ ] `IRealtimeTranslationProvider` — OpenAI çeviri motoru
- [ ] Translation Router: LibreTranslate ↔ OpenAI Translate seçimi
- [ ] TR konuşması → EN metin overlay
- [ ] Opsiyonel virtual microphone araştırması
- [ ] İş görüşmesi için varsayılan kapalı mod

## Yeni Mimari Katmanlar (V2)

```
Audio Capture → Preprocess → VAD
  → STT Router (local | OpenAI Realtime)
  → Translation Router (local | OpenAI)
  → Quality Gate (confidence, glossary, uyarı)
  → Assistant Layer (özet, intent, cevap kartı)
  → Overlay UI
  → Session Privacy (bulut API uyarısı, kayıt kapalı)
```

## Gizlilik Kararları (V2)

- Varsayılan mod Local Free Mode olarak kalır
- OpenAI modu açılırken "ses/transcript bulut API'ye gönderilir" uyarısı zorunlu
- API key kullanıcıya aittir — uygulamaya gömülmez
- Debug log maskeleme yapılmalı
