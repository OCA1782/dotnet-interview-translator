namespace InterviewTranslator.Shared.Models;

public sealed class InterviewAssistResult
{
    public string TurkishSummary     { get; init; } = "";
    /// teknik | davranışsal | deneyim | maaş | genel
    public string DetectedIntent     { get; init; } = "genel";
    public string SuggestedAnswerKey { get; init; } = "";
    public double Confidence         { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public static InterviewAssistResult Empty => new();
}
