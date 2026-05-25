using InterviewTranslator.Shared.Models;
using InterviewTranslator.Vad;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InterviewTranslator.Tests;

public class EnergyVadTests
{
    private readonly EnergyBasedVadService _sut = new(NullLogger<EnergyBasedVadService>.Instance);

    [Fact]
    public async Task DetectSpeech_AllSilence_ProducesNoSegments()
    {
        var frames = GenerateFrames(isSpeech: false, count: 30).ToAsyncEnumerable();
        var segments = await _sut.DetectSpeechAsync(frames, CancellationToken.None).ToListAsync();
        Assert.Empty(segments);
    }

    [Fact]
    public async Task DetectSpeech_SpeechThenSilence_ProducesOneSegment()
    {
        var frames = GenerateFrames(isSpeech: true, count: 10)
            .Concat(GenerateFrames(isSpeech: false, count: 20))
            .ToAsyncEnumerable();

        var segments = await _sut.DetectSpeechAsync(frames, CancellationToken.None).ToListAsync();
        Assert.Single(segments);
    }

    [Fact]
    public async Task DetectSpeech_TwoSpeechBlocks_ProducesTwoSegments()
    {
        var frames = GenerateFrames(isSpeech: true, count: 8)
            .Concat(GenerateFrames(isSpeech: false, count: 20))
            .Concat(GenerateFrames(isSpeech: true, count: 8))
            .Concat(GenerateFrames(isSpeech: false, count: 20))
            .ToAsyncEnumerable();

        var segments = await _sut.DetectSpeechAsync(frames, CancellationToken.None).ToListAsync();
        Assert.Equal(2, segments.Count);
    }

    [Fact]
    public async Task DetectSpeech_SegmentHasSampleRate()
    {
        var frames = GenerateFrames(isSpeech: true, count: 10)
            .Concat(GenerateFrames(isSpeech: false, count: 20))
            .ToAsyncEnumerable();

        var segments = await _sut.DetectSpeechAsync(frames, CancellationToken.None).ToListAsync();
        Assert.Equal(16000, segments[0].SampleRate);
    }

    // 50ms @ 16kHz mono 16-bit = 1600 bytes
    private static IEnumerable<AudioFrame> GenerateFrames(bool isSpeech, int count)
    {
        var rng = new Random(42);
        for (int i = 0; i < count; i++)
        {
            var pcm = new byte[1600];
            if (isSpeech)
            {
                // Yüksek amplitüdlü sinyal (konuşma simülasyonu)
                for (int j = 0; j < pcm.Length - 1; j += 2)
                {
                    short sample = (short)(short.MaxValue * 0.5 * Math.Sin(j * 0.1));
                    pcm[j]     = (byte)(sample & 0xFF);
                    pcm[j + 1] = (byte)((sample >> 8) & 0xFF);
                }
            }
            // Sessizlik: sıfır PCM (default)
            yield return new AudioFrame
            {
                Pcm16 = pcm,
                SampleRate = 16000,
                Channels = 1,
                Timestamp = DateTimeOffset.UtcNow
            };
        }
    }
}

// IAsyncEnumerable yardımcıları
file static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source) yield return item;
        await Task.CompletedTask;
    }

    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct = default)
    {
        var list = new List<T>();
        await foreach (var item in source.WithCancellation(ct))
            list.Add(item);
        return list;
    }
}
