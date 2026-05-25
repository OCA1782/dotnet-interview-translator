using InterviewTranslator.Shared.Contracts;

namespace InterviewTranslator.Subtitles;

public sealed class SuggestionPublisher : ISuggestionPublisher
{
    private readonly SuggestionBuffer _buffer;

    public SuggestionPublisher(SuggestionBuffer buffer) => _buffer = buffer;

    public Task PublishAsync(SuggestionItem item, CancellationToken cancellationToken)
    {
        _buffer.Add(item);
        return Task.CompletedTask;
    }
}
