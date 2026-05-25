using System.Runtime.CompilerServices;
using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Shared.Models;
using InterviewTranslator.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InterviewTranslator.Vad;

public sealed class EnergyBasedVadService : IVadService
{
    private readonly VadOptions _vad;
    private readonly ILogger<EnergyBasedVadService> _logger;

    public EnergyBasedVadService(IOptions<AppOptions> options, ILogger<EnergyBasedVadService> logger)
    {
        _vad    = options.Value.Vad;
        _logger = logger;
    }

    public async IAsyncEnumerable<SpeechSegment> DetectSpeechAsync(
        IAsyncEnumerable<AudioFrame> frames,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var speechBuffer = new List<byte>();
        int silenceCount = 0, speechCount = 0;
        bool inSpeech    = false;
        DateTimeOffset segmentStart = default;
        int sampleRate   = 16000;
        int frameMs      = 30;
        int diagFrame    = 0;
        double maxEnergy = 0;

        await foreach (var frame in frames.WithCancellation(cancellationToken))
        {
            sampleRate = frame.SampleRate;
            // Derive frame duration from actual PCM length — frame-size agnostic
            frameMs = frame.Pcm16.Length * 1000 / (frame.SampleRate * 2);

            // Live-readable thresholds — user can change in Settings without restart
            double threshold       = _vad.SpeechThreshold;
            int silenceFramesEnd   = Math.Max(1, _vad.SilenceMs  / Math.Max(1, frameMs));
            int minSpeechFrames    = Math.Max(1, _vad.MinSpeechMs / Math.Max(1, frameMs));
            int maxSpeechFrames    = Math.Max(2, _vad.MaxSpeechMs / Math.Max(1, frameMs));

            double energy   = ComputeRmsEnergy(frame.Pcm16);
            bool   isSpeech = energy > threshold;

            diagFrame++;
            if (energy > maxEnergy) maxEnergy = energy;
            if (diagFrame % 200 == 0)
            {
                _logger.LogInformation(
                    "[VAD] Max enerji ({Sec}s): {Level:P1} — eşik: {Thr:P1} | silence:{S}ms min:{Min}ms max:{Max}ms",
                    diagFrame * frameMs / 1000,
                    maxEnergy, threshold,
                    _vad.SilenceMs, _vad.MinSpeechMs, _vad.MaxSpeechMs);
                maxEnergy = 0;
            }

            if (!inSpeech)
            {
                if (isSpeech)
                {
                    inSpeech     = true;
                    silenceCount = 0;
                    speechCount  = 1;
                    segmentStart = frame.Timestamp;
                    speechBuffer.Clear();
                    speechBuffer.AddRange(frame.Pcm16);
                }
            }
            else
            {
                speechBuffer.AddRange(frame.Pcm16);

                if (isSpeech) { speechCount++; silenceCount = 0; }
                else           silenceCount++;

                bool hitSilence = silenceCount >= silenceFramesEnd;
                bool hitMax     = (speechCount + silenceCount) >= maxSpeechFrames;

                if (hitSilence || hitMax)
                {
                    inSpeech = false;
                    if (speechCount >= minSpeechFrames)
                    {
                        double durSec = speechCount * frameMs / 1000.0;
                        _logger.LogInformation(
                            "[VAD] Segment [{Reason}] — {Dur:F1}s",
                            hitMax ? "MAX" : "sessizlik", durSec);

                        yield return new SpeechSegment
                        {
                            Pcm16Mono  = speechBuffer.ToArray(),
                            SampleRate = sampleRate,
                            Duration   = TimeSpan.FromSeconds(durSec),
                            StartedAt  = segmentStart,
                            EndedAt    = frame.Timestamp
                        };
                    }

                    speechBuffer.Clear();
                    speechCount  = 0;
                    silenceCount = 0;
                }
            }
        }

        if (inSpeech && speechCount >= Math.Max(1, _vad.MinSpeechMs / Math.Max(1, frameMs))
                     && speechBuffer.Count > 0)
        {
            double durSec = speechCount * frameMs / 1000.0;
            yield return new SpeechSegment
            {
                Pcm16Mono  = speechBuffer.ToArray(),
                SampleRate = sampleRate,
                Duration   = TimeSpan.FromSeconds(durSec),
                StartedAt  = segmentStart,
                EndedAt    = DateTimeOffset.UtcNow
            };
        }
    }

    private static double ComputeRmsEnergy(byte[] pcm16)
    {
        if (pcm16.Length < 2) return 0;
        long sum   = 0;
        int  count = pcm16.Length / 2;
        for (int i = 0; i < pcm16.Length - 1; i += 2)
        {
            short s = (short)(pcm16[i] | (pcm16[i + 1] << 8));
            sum += (long)s * s;
        }
        return Math.Sqrt((double)sum / count) / short.MaxValue;
    }
}
