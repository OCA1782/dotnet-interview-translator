# InterviewTranslator — Docs

Canlı çeviri uygulamasının teknik dokümanları ve içerik verileri.

## Dosyalar

| Dosya | Açıklama |
|-------|----------|
| `answer-cards.json` | Uygulama cevap kartları — uygulama bu dosyadan okur |
| `glossary.json` | Teknik terimler sözlüğü — çeviri kalitesini artırır |
| `mvp-plan.md` | Geliştirme planı ve FAZ takibi |
| `architecture.md` | Teknik mimari ve bileşen açıklamaları |

## Cevap Kartı Güncelleme

`answer-cards.json` dosyasını düzenleyip `main` branch'e push ettiğinizde,
uygulama bir sonraki **"GitHub'dan Yenile"** işleminde yeni kartları otomatik yükler.

Kart formatı:
```json
{ "category": "technical|behavioral|general", "topic": "...", "answer": "..." }
```

## Raw URL'ler (uygulama kullanır)

```
https://raw.githubusercontent.com/OCA1782/dotnet-interview-translator-docs/main/answer-cards.json
https://raw.githubusercontent.com/OCA1782/dotnet-interview-translator-docs/main/glossary.json
```
