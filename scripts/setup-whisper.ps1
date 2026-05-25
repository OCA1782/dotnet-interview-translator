# whisper.cpp model indirme scripti
# Model otomatik indirilir ama manuel ön-indirme için kullanılabilir

$modelDir = "$PSScriptRoot\..\workers\models"
$modelFile = "$modelDir\ggml-base.en.bin"
$modelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin"

if (Test-Path $modelFile) {
    Write-Host "Model zaten mevcut: $modelFile"
    exit 0
}

New-Item -ItemType Directory -Force -Path $modelDir | Out-Null
Write-Host "Model indiriliyor (~142 MB)..."
Invoke-WebRequest -Uri $modelUrl -OutFile $modelFile -UseBasicParsing
Write-Host "Model indirildi: $modelFile"
