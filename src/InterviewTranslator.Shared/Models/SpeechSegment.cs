namespace InterviewTranslator.Shared.Models;

public sealed class SpeechSegment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required byte[] Pcm16Mono { get; init; }
    public int SampleRate { get; init; }
    public TimeSpan Duration { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset EndedAt { get; init; }
}
