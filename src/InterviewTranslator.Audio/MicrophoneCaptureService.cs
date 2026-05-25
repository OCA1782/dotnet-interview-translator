using System.Runtime.CompilerServices;
using System.Threading.Channels;
using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Shared.Models;
using InterviewTranslator.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace InterviewTranslator.Audio;

public sealed class MicrophoneCaptureService : IMicrophoneCaptureService
{
    private readonly ILogger<MicrophoneCaptureService> _logger;
    private string? _deviceId;

    private const int OutRate   = 16000;
    private const int ChunkMs   = 30;
    private const int ChunkBytes = OutRate * 2 * ChunkMs / 1000; // 960 byte

    public MicrophoneCaptureService(IOptions<AppOptions> options, ILogger<MicrophoneCaptureService> logger)
    {
        _logger   = logger;
        _deviceId = options.Value.Microphone?.PreferredDeviceId;
    }

    public IReadOnlyList<AudioDeviceInfo> GetInputDevices()
    {
        using var en = new MMDeviceEnumerator();
        return en.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                 .Select(d => new AudioDeviceInfo { Id = d.ID, Name = d.FriendlyName })
                 .ToList();
    }

    public void SetDevice(string deviceId) => _deviceId = deviceId;

    public async IAsyncEnumerable<AudioFrame> CaptureAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<AudioFrame>(new BoundedChannelOptions(400)
        {
            FullMode     = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });

        int deviceNumber = ResolveDeviceNumber();

        // 16kHz'yi doğrudan dene; desteklenmiyorsa 44100Hz + resampler
        var (capture, nativeFormat, needsResample) = TryOpen16kHz(deviceNumber)
            ?? OpenNative44k(deviceNumber);

        BufferedWaveProvider? waveBuf = null;
        MediaFoundationResampler? resampler = null;

        if (needsResample)
        {
            waveBuf   = new BufferedWaveProvider(nativeFormat)
            {
                BufferDuration          = TimeSpan.FromSeconds(5),
                DiscardOnBufferOverflow = true
            };
            resampler = new MediaFoundationResampler(waveBuf, new WaveFormat(OutRate, 16, 1))
            {
                ResamplerQuality = 1
            };
        }

        var readBuf  = new byte[ChunkBytes];
        var accum    = new byte[ChunkBytes * 4];
        int accumLen = 0;

        if (!needsResample)
        {
            // 16kHz doğrudan: DataAvailable → channel
            capture.DataAvailable += (_, e) =>
            {
                if (e.BytesRecorded == 0) return;
                int src = 0;
                while (src < e.BytesRecorded)
                {
                    int copy = Math.Min(e.BytesRecorded - src, ChunkBytes - accumLen);
                    Array.Copy(e.Buffer, src, accum, accumLen, copy);
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
            };
        }
        else
        {
            // 44100Hz: DataAvailable → waveBuf
            capture.DataAvailable += (_, e) =>
            {
                if (e.BytesRecorded > 0)
                    waveBuf!.AddSamples(e.Buffer, 0, e.BytesRecorded);
            };
        }

        capture.RecordingStopped += (_, _) => channel.Writer.TryComplete();

        _logger.LogInformation("[MIC] Başlatıldı — format: {Fmt}, resample: {R}",
            nativeFormat, needsResample);
        capture.StartRecording();

        Task? readTask = null;
        if (needsResample)
        {
            readTask = Task.Run(() =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        int read = resampler!.Read(readBuf, 0, readBuf.Length);
                        if (read == 0) { Thread.Sleep(2); continue; }

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
                finally { channel.Writer.TryComplete(); }
            }, cancellationToken);
        }

        try
        {
            await foreach (var frame in channel.Reader.ReadAllAsync(cancellationToken))
                yield return frame;
        }
        finally
        {
            capture.StopRecording();
            if (readTask is not null)
                await readTask.ConfigureAwait(false);
            resampler?.Dispose();
            capture.Dispose();
            _logger.LogInformation("[MIC] Durduruldu.");
        }
    }

    private int ResolveDeviceNumber()
    {
        if (_deviceId is null) return 0;
        using var en = new MMDeviceEnumerator();
        var devices  = en.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
        for (int i = 0; i < devices.Count; i++)
            if (devices[i].ID == _deviceId) return i;
        return 0;
    }

    private (WaveInEvent, WaveFormat, bool)? TryOpen16kHz(int deviceNumber)
    {
        try
        {
            var fmt  = new WaveFormat(OutRate, 16, 1);
            var test = new WaveInEvent { DeviceNumber = deviceNumber, WaveFormat = fmt };
            test.StartRecording();
            test.StopRecording();
            test.Dispose();

            var cap = new WaveInEvent
            {
                DeviceNumber       = deviceNumber,
                WaveFormat         = fmt,
                BufferMilliseconds = ChunkMs
            };
            _logger.LogInformation("[MIC] Native 16kHz destekleniyor — resampling yok.");
            return (cap, fmt, false);
        }
        catch
        {
            return null;
        }
    }

    private (WaveInEvent, WaveFormat, bool) OpenNative44k(int deviceNumber)
    {
        var fmt = new WaveFormat(44100, 16, 1);
        var cap = new WaveInEvent
        {
            DeviceNumber       = deviceNumber,
            WaveFormat         = fmt,
            BufferMilliseconds = 50
        };
        _logger.LogInformation("[MIC] 44100Hz — MediaFoundationResampler aktif.");
        return (cap, fmt, true);
    }
}
