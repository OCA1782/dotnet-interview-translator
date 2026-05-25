using System.Runtime.CompilerServices;
using System.Threading.Channels;
using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Shared.Models;
using InterviewTranslator.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace InterviewTranslator.Audio;

public sealed class WasapiLoopbackCaptureService : IAudioCaptureService, IDisposable
{
    private readonly AudioOptions _options;
    private readonly ILogger<WasapiLoopbackCaptureService> _logger;
    private string? _deviceId;

    private const int   OutRate    = 16000;
    private const int   ChunkMs   = 30;
    private const int   ChunkBytes = OutRate * 2 * ChunkMs / 1000; // 960 byte
    private readonly AudioProcessingOptions _proc;

    public WasapiLoopbackCaptureService(IOptions<AppOptions> options, ILogger<WasapiLoopbackCaptureService> logger)
    {
        _options  = options.Value.Audio;
        _proc     = options.Value.AudioProcessing;
        _logger   = logger;
        _deviceId = _options.PreferredDeviceId;
    }

    public IReadOnlyList<AudioDeviceInfo> GetOutputDevices()
    {
        using var en = new MMDeviceEnumerator();
        return en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                 .Select(d => new AudioDeviceInfo { Id = d.ID, Name = d.FriendlyName })
                 .ToList();
    }

    public void SetDevice(string deviceId) => _deviceId = deviceId;

    public async IAsyncEnumerable<AudioFrame> CaptureAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var device    = FindActiveDevice();
        using var cap = new WasapiLoopbackCapture(device);
        var inFmt     = cap.WaveFormat;

        var waveBuf = new BufferedWaveProvider(inFmt)
        {
            BufferDuration          = TimeSpan.FromSeconds(5),
            DiscardOnBufferOverflow = true
        };

        // Pure NAudio pipeline — avoids MediaFoundationResampler which silently
        // outputs zeros for IEEFloat stereo input on some Windows configurations.
        // IEEFloat stereo NkHz → float mono NkHz → float mono 16kHz → int16 mono 16kHz
        ISampleProvider samplePipeline = waveBuf.ToSampleProvider();
        if (inFmt.Channels > 1)
            samplePipeline = samplePipeline.ToMono();
        if (inFmt.SampleRate != OutRate)
            samplePipeline = new WdlResamplingSampleProvider(samplePipeline, OutRate);
        IWaveProvider pcmProvider = new SampleToWaveProvider16(samplePipeline);

        var channel = Channel.CreateBounded<AudioFrame>(new BoundedChannelOptions(400)
        {
            FullMode     = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });

        var readBuf = new byte[ChunkBytes];
        var accum   = new byte[ChunkBytes * 4];
        int accumLen = 0;

        // Ses verisi geldiğinde buffer'a yaz
        cap.DataAvailable += (_, e) =>
        {
            if (e.BytesRecorded > 0)
                waveBuf.AddSamples(e.Buffer, 0, e.BytesRecorded);
        };

        cap.RecordingStopped += (_, _) => channel.Writer.TryComplete();

        _logger.LogInformation("[LOOP] Başlatıldı → {Device} | giriş: {Fmt}",
            device.FriendlyName, inFmt);
        cap.StartRecording();

        // Ayrı thread'de PCM provider'dan oku, Channel'a yaz
        var readTask = Task.Run(() =>
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    int read = pcmProvider.Read(readBuf, 0, readBuf.Length);
                    if (read == 0)
                    {
                        Thread.Sleep(2);
                        continue;
                    }

                    // Gain — options'tan canlı okunur (Settings'ten anında değişir)
                    float gain = _proc.LoopbackGain;
                    if (Math.Abs(gain - 1.0f) > 0.01f)
                    {
                        for (int i = 0; i < read - 1; i += 2)
                        {
                            short s = (short)(readBuf[i] | (readBuf[i + 1] << 8));
                            int   a = (int)Math.Round(s * gain);
                            a       = Math.Clamp(a, short.MinValue, short.MaxValue);
                            readBuf[i]     = (byte)(a & 0xFF);
                            readBuf[i + 1] = (byte)((a >> 8) & 0xFF);
                        }
                    }

                    // ChunkBytes boyutunda frame'ler üret
                    int src = 0;
                    while (src < read)
                    {
                        int copy = Math.Min(read - src, ChunkBytes - accumLen);
                        Array.Copy(readBuf, src, accum, accumLen, copy);
                        accumLen += copy;
                        src      += copy;

                        if (accumLen >= ChunkBytes)
                        {
                            var frame = new byte[ChunkBytes];
                            Array.Copy(accum, frame, ChunkBytes);
                            channel.Writer.TryWrite(new AudioFrame
                            {
                                Pcm16      = frame,
                                SampleRate = OutRate,
                                Channels   = 1,
                                Timestamp  = DateTimeOffset.UtcNow
                            });
                            accumLen = 0;
                        }
                    }
                }
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, cancellationToken);

        try
        {
            await foreach (var frame in channel.Reader.ReadAllAsync(cancellationToken))
                yield return frame;
        }
        finally
        {
            cap.StopRecording();
            await readTask.ConfigureAwait(false);
            _logger.LogInformation("[LOOP] Durduruldu.");
        }
    }

    private MMDevice FindActiveDevice()
    {
        using var en = new MMDeviceEnumerator();

        if (_deviceId is not null)
        {
            try
            {
                var sel = en.GetDevice(_deviceId);
                _logger.LogInformation("[LOOP] Kullanıcı seçimi: {D}", sel.FriendlyName);
                return sel;
            }
            catch { }
        }

        var devices  = en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
        MMDevice? best = null;
        float bestPeak = -1f;

        foreach (var d in devices)
        {
            float peak = 0f;
            try { peak = d.AudioMeterInformation.MasterPeakValue; }
            catch { }
            _logger.LogInformation("[LOOP]  [{Peak:P1}] {Name}", peak, d.FriendlyName);
            if (peak > bestPeak) { bestPeak = peak; best = d; }
        }

        if (best is not null && bestPeak > 0.005f)
        {
            _logger.LogInformation("[LOOP] Aktif cihaz: {D} (peak {P:P1})", best.FriendlyName, bestPeak);
            return best;
        }

        var def = en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        _logger.LogWarning("[LOOP] Aktif ses yok — default kullanılıyor: {D}", def.FriendlyName);
        return def;
    }

    public void Dispose() { }
}
