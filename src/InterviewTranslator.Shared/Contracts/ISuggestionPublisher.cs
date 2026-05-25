namespace InterviewTranslator.Shared.Contracts;

public interface ISuggestionPublisher
{
    Task PublishAsync(SuggestionItem item, CancellationToken cancellationToken);
}

public sealed class SuggestionItem
{
    public Guid SegmentId { get; init; }
    public string TurkishText { get; init; } = "";
    public string EnglishSuggestion { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
}
