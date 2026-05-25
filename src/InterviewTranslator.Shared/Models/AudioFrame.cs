namespace InterviewTranslator.Shared.Models;

public sealed class AudioFrame
{
    public required byte[] Pcm16 { get; init; }
    public int SampleRate { get; init; }
    public int Channels { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
