# LibreTranslate yerel kurulumu
# Gereksinim: Docker Desktop çalışıyor olmalı

$containerName = "libretranslate"
$port = 5000

$running = docker ps --filter "name=$containerName" --format "{{.Names}}" 2>$null

if ($running -eq $containerName) {
    Write-Host "LibreTranslate zaten çalışıyor: http://localhost:$port"
    exit 0
}

Write-Host "LibreTranslate başlatılıyor (EN + TR modeli)..."
docker run -d `
    --name $containerName `
    -p "${port}:5000" `
    -e LT_LOAD_ONLY=en,tr `
    libretranslate/libretranslate

Write-Host "Başlatıldı: http://localhost:$port"
Write-Host "İlk çalıştırmada model indirme birkaç dakika sürebilir."
