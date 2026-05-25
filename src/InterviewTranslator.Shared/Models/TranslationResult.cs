namespace InterviewTranslator.Shared.Models;

public sealed class TranslationResult
{
    public Guid SegmentId { get; init; }
    public string SourceText { get; init; } = "";
    public string TranslatedText { get; init; } = "";
    public string SourceLanguage { get; init; } = "en";
    public string TargetLanguage { get; init; } = "tr";
}
