using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Shared.Models;

namespace InterviewTranslator.Subtitles;

public sealed class SubtitlePublisher : ISubtitlePublisher
{
    private readonly SubtitleBuffer _buffer;

    public SubtitlePublisher(SubtitleBuffer buffer)
    {
        _buffer = buffer;
    }

    public Task PublishAsync(SubtitleItem item, CancellationToken cancellationToken)
    {
        _buffer.AddOrUpdate(item);
        return Task.CompletedTask;
    }
}
