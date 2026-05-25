# Teknik Terim Sözlüğü

GlossaryService çeviriden önce bu terimleri korur (placeholder ile değiştirir), çeviriden sonra geri yükler.
Böylece "deployment" → "konuşlandırma" gibi yanlış çeviriler önlenir.

Toplam: **130+ terim**  
Öncelik: Uzun çok kelimeli terimler önce işlenir (örn. "microservices architecture" > "microservice").

## Kategoriler

| Kategori | Örnek Terimler |
|----------|---------------|
| Mimari & Tasarım | microservice, event-driven architecture, CQRS, circuit breaker |
| DevOps & CI/CD | CI/CD pipeline, blue-green deployment, canary deployment, rollback |
| Konteyner | Kubernetes, Docker, Helm, pod, ingress, sidecar |
| Bulut | AWS, Azure, GCP, serverless, Terraform, auto-scaling |
| Yazılım Geliştirme | pull request, code review, TDD, BDD, dependency injection, hotfix |
| API & Protokoller | REST API, GraphQL, gRPC, WebSocket, OAuth, JWT |
| Veri & Depolama | PostgreSQL, MongoDB, Redis, Kafka, RabbitMQ, sharding |
| Performans | throughput, latency, P99, SLA, SLO, observability |
| Proje & Metodoloji | Agile, Scrum, sprint, backlog, MVP, OKR |

## Yeni Terim Eklemek

`GlossaryService.cs` içindeki `TermList` dizisine şu formatla ekle:

```csharp
("çok kelimeli terim", "Canonical Form"),   // çok kelimeli → önce
("tek terim",          "Canonical Form"),   // tek kelimeli → sonra
```

Uzun terimler kısa terimlerden **önce** tanımlanmalı.

## VAD Parametreleri (Ayarlar → VAD Sekmesi)

| Parametre | Varsayılan | Açıklama |
|-----------|-----------|----------|
| SpeechThreshold | 0.003 (0.3%) | RMS enerji eşiği; düşük=hassas, yüksek=gürültü filtresi |
| SilenceMs | 240 ms | Sessizlik sonrası segment gönderme gecikmesi |
| MinSpeechMs | 120 ms | Bu süreden kısa sesleri yoksay |
| MaxSpeechMs | 2000 ms | Bu süre dolduğunda zorla segment gönder |

Hızlı Önayarlar:

| Önayar | Threshold | Silence | MinSpeech | MaxSpeech |
|--------|-----------|---------|-----------|-----------|
| Duyarlı | 0.001 | 200 ms | 80 ms | 3000 ms |
| Dengeli (varsayılan) | 0.003 | 240 ms | 120 ms | 2000 ms |
| Seçici (gürültülü) | 0.008 | 400 ms | 200 ms | 2500 ms |

## Whisper Model Seçimi (Ayarlar → Whisper Sekmesi)

| Model | Boyut | Yaklaşık Gecikme | Kullanım |
|-------|-------|-----------------|---------|
| tiny.en | 39 MB | ~0.3s | EN→TR hızlı, yeterli doğruluk |
| base.en | 148 MB | ~0.8s | EN→TR dengeli |
| small.en | 488 MB | ~2s | EN→TR yüksek doğruluk |
| tiny | 39 MB | ~0.3s | TR→EN hızlı |
| base | 148 MB | ~0.8s | TR→EN dengeli |
| small | 488 MB | ~2s | TR→EN yüksek doğruluk |

Model değiştirdikten sonra "Whisper Processor'ı Sıfırla" butonuna basılmalıdır.
