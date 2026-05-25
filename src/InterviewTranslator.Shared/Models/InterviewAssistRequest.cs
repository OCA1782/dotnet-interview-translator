namespace InterviewTranslator.Shared.Models;

public sealed class InterviewAssistRequest
{
    public string OriginalEnglish   { get; init; } = "";
    public string TranslatedTurkish { get; init; } = "";
    public string Domain            { get; init; } = "software engineering";
}
