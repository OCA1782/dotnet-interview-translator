namespace InterviewTranslator.Shared.Models;

public sealed class TranscriptionResult
{
    public Guid SegmentId { get; init; }
    public string SourceLanguage { get; init; } = "en";
    public string Text { get; init; } = "";
    public double? Confidence { get; init; }
    public bool IsFinal { get; init; }
}
