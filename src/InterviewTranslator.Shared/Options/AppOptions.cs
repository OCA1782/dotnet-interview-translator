namespace InterviewTranslator.Shared.Options;

public sealed class AppOptions
{
    public AudioOptions           Audio              { get; set; } = new();
    public AudioProcessingOptions AudioProcessing    { get; set; } = new();
    public MicrophoneOptions      Microphone         { get; set; } = new();
    public VadOptions             Vad                { get; set; } = new();
    public SttOptions             Stt                { get; set; } = new();
    public SttOptions             MicStt             { get; set; } = new()
    {
        ModelPath  = "workers/models/ggml-tiny.bin",
        Language   = "auto",
        ModelType  = "tiny"
    };
    public TranslationOptions     Translation        { get; set; } = new();
    public TranslationOptions     ReverseTranslation { get; set; } = new()
    {
        SourceLanguage = "tr",
        TargetLanguage = "en"
    };
    public OverlayOptions         Overlay            { get; set; } = new();
    public TtsOptions             Tts                { get; set; } = new();
    public PrivacyOptions         Privacy            { get; set; } = new();
    public OpenAIOptions          OpenAI             { get; set; } = new();
    public GitHubDocsOptions      GitHubDocs         { get; set; } = new();
}

// ──────────────── Audio ────────────────

public sealed class AudioOptions
{
    public string? PreferredDeviceId { get; set; }
    public int     SampleRate        { get; set; } = 16000;
    public int     Channels          { get; set; } = 1;
}

public sealed class MicrophoneOptions
{
    public string? PreferredDeviceId { get; set; }
}

public sealed class AudioProcessingOptions
{
    /// Loopback ses yükseltme çarpanı (1x = orijinal, 4x = 4 kat)
    public float LoopbackGain     { get; set; } = 3.0f;
    /// MediaFoundationResampler kalitesi: 1=hızlı/düşük gecikme, 60=yüksek kalite
    public int   ResamplerQuality { get; set; } = 1;
    /// Ses frame boyutu (ms): küçük=hassas VAD, büyük=daha az CPU
    public int   ChunkMs          { get; set; } = 30;
}

// ──────────────── VAD ────────────────

public sealed class VadOptions
{
    /// RMS enerji eşiği (0.001–0.020) — düşük=hassas, yüksek=sadece net ses
    public double SpeechThreshold { get; set; } = 0.003;
    /// Sessizlik sonrası segment gönderme gecikmesi (ms)
    public int SilenceMs          { get; set; } = 240;
    /// Bu süreden kısa sesleri yoksay — gürültü filtresi (ms)
    public int MinSpeechMs        { get; set; } = 120;
    /// Bu süreden uzun segmentleri zorla böl (ms)
    public int MaxSpeechMs        { get; set; } = 2000;
}

// ──────────────── STT ────────────────

public sealed class SttOptions
{
    public string ModelPath          { get; set; } = "workers/models/ggml-tiny.en.bin";
    public string Language           { get; set; } = "en";
    /// Kullanıcı arayüzünde gösterilen model adı
    public string ModelType          { get; set; } = "tiny.en";
    /// Sessizlik olasılığı eşiği — üstündeyse segment yoksayılır
    public float  NoSpeechThreshold  { get; set; } = 0.4f;
    /// 0.0 = deterministik, 0.5 = yaratıcı
    public float  Temperature        { get; set; } = 0.0f;
}

// ──────────────── Translation ────────────────

public sealed class TranslationOptions
{
    public string LibreTranslateUrl { get; set; } = "http://localhost:5050";
    public string SourceLanguage    { get; set; } = "en";
    public string TargetLanguage    { get; set; } = "tr";
    public int    TimeoutSeconds    { get; set; } = 10;
}

// ──────────────── Overlay ────────────────

public sealed class OverlayOptions
{
    public double FontSize    { get; set; } = 18;
    public double Opacity     { get; set; } = 0.85;
    public bool   ShowEnglish { get; set; } = true;
    public int    MaxLines    { get; set; } = 3;
}

// ──────────────── TTS ────────────────

public sealed class TtsOptions
{
    public bool Enabled { get; set; } = false;
    public int  Rate    { get; set; } = 0;
    public int  Volume  { get; set; } = 100;
}

// ──────────────── Privacy ────────────────

public sealed class PrivacyOptions
{
    public bool SaveAudio      { get; set; } = false;
    public bool SaveTranscript { get; set; } = false;
    public bool DebugLog       { get; set; } = false;
}

// ──────────────── GitHub Docs ────────────────

public sealed class GitHubDocsOptions
{
    public string RawBaseUrl      { get; set; } = "https://raw.githubusercontent.com/OCA1782/dotnet-interview-translator-docs/main";
    public int    TimeoutSeconds  { get; set; } = 10;
}

// ──────────────── OpenAI (V2) ────────────────

public enum OpenAIMode { Disabled, AssistantOnly, HybridSTT, FullCloud }

public sealed class OpenAIOptions
{
    public string      ApiKey         { get; set; } = "";
    public OpenAIMode  Mode           { get; set; } = OpenAIMode.Disabled;
    /// GPT modeli: anlam özeti + intent tespiti için
    public string      AssistantModel { get; set; } = "gpt-4o-mini";
    public int         MaxTokens      { get; set; } = 300;
    public int         TimeoutSeconds { get; set; } = 8;
    public bool        ShowCloudWarning { get; set; } = true;
}
