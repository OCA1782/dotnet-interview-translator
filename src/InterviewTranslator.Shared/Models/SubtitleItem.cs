namespace InterviewTranslator.Shared.Models;

public sealed class SubtitleItem
{
    public Guid SegmentId { get; init; }
    public string EnglishText { get; init; } = "";
    public string TurkishText { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public TimeSpan Latency { get; init; }
}
