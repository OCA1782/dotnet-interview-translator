# InterviewTranslator — Canlı Çeviri Uygulaması

## Proje Özeti
Windows masaüstü uygulaması. İngilizce konuşmayı sistem sesinden (WASAPI Loopback) gerçek zamanlı yakalar,
Whisper.cpp ile metne çevirir, LibreTranslate ile Türkçeye çevirir ve ekran üzerinde overlay altyazı gösterir.
OpenAI GPT-4o-mini entegrasyonu ile akıllı cevap önerileri sunar.

**Konum:** `C:\PROJECTS\DOTNET\InterviewTranslator`  
**Takip:** `dev_log.txt`  
**Docs repo:** https://github.com/OCA1782/dotnet-interview-translator-docs  
**Kod repo:** https://github.com/OCA1782/dotnet-interview-translator  

---

## Mimari

```
src/
  InterviewTranslator.Shared      → DTO'lar, interface'ler, AppOptions
  InterviewTranslator.Audio       → WASAPI Loopback + Mikrofon yakalama (NAudio)
  InterviewTranslator.Vad         → Energy-based VAD (konuşma/sessizlik)
  InterviewTranslator.Stt         → Whisper.cpp STT (Whisper.net)
  InterviewTranslator.Translate   → LibreTranslate HTTP + GlossaryService
  InterviewTranslator.Subtitles   → SubtitleBuffer, SubtitlePublisher
  InterviewTranslator.OpenAI      → GPT-4o-mini Assistant entegrasyonu
  InterviewTranslator.Desktop     → WPF UI, Orchestrator'lar, Overlay pencereler

docs/
  answer-cards.json   → Hazır cevap kartları (GitHub'dan güncellenir)
  glossary.json       → Teknik terimler sözlüğü
  mvp-plan.md         → FAZ takibi
  architecture.md     → Teknik mimari

workers/
  models/             → Whisper GGML model dosyaları (.bin) — .gitignore'da
  libretranslate/     → LibreTranslate runtime — .gitignore'da

scripts/
  setup-whisper.ps1       → Model indirme scripti
  setup-libretranslate.ps1 → LibreTranslate Docker kurulumu
```

## Teknoloji Kararları

| Alan | Karar |
|------|-------|
| Ses yakalama | NAudio WASAPI Loopback (sistem sesi) + WaveInEvent (mikrofon) |
| STT | Whisper.net (whisper.cpp binding) — tiny.en + tiny modelleri |
| Çeviri | LibreTranslate (lokal Docker, ücretsiz, private) |
| UI | WPF (.NET 8), ModernTheme, Overlay pencere (Topmost) |
| Asistan | OpenAI GPT-4o-mini, AssistantOnly modu |
| Docs | GitHub raw content — `GitHubDocsService` ile çekilir |

## Kod Kuralları

- async/await zorunlu; her servis metoduna `CancellationToken` geçmeli
- `WaveInEvent` (NAudio) Win32 thread affinity: oluşturulduğu thread'de kalmalı, `Task.Run`'a sarılmaz
- STT segment limiti: `Interlocked.CompareExchange` ile max 1 eşzamanlı segment (kuyruk oluşmaz)
- Whisper context: önceki transcript `WithPrompt()` ile geçirilir (kısa segment doğruluğu için)
- VAD parametreleri: `MaxSpeechMs=800` → rolling real-time his
- API key'ler `appsettings.json`'a yazılmaz, `.gitignore`'da tutulur
- Git: commit mesajlarına NEXUS ile ilgili hiçbir bilgi yazılmaz

## Önemli Dosyalar

| Dosya | Açıklama |
|-------|----------|
| `src/InterviewTranslator.Desktop/LiveTranslationOrchestrator.cs` | Loopback → VAD → STT → Translate → Overlay pipeline |
| `src/InterviewTranslator.Desktop/BidirectionalOrchestrator.cs` | Mikrofon → TR→EN pipeline |
| `src/InterviewTranslator.Desktop/GitHubDocsService.cs` | GitHub'dan answer-cards.json çeker |
| `src/InterviewTranslator.Stt/WhisperCppSttEngine.cs` | Loopback STT (tiny.en, context prompt) |
| `src/InterviewTranslator.Stt/MicWhisperSttEngine.cs` | Mikrofon STT (tiny, auto-detect dil) |
| `src/InterviewTranslator.Shared/Options/AppOptions.cs` | Tüm konfigürasyon sınıfları |
| `src/InterviewTranslator.Desktop/appsettings.json` | Çalışma zamanı ayarları |

## Session Başlangıcı

1. `dev_log.txt` oku — son FAZ durumunu anla
2. `docs/mvp-plan.md` oku — aktif görevlere bak
3. Değişiklik varsa build et: `dotnet build src/InterviewTranslator.Desktop/InterviewTranslator.Desktop.csproj`
