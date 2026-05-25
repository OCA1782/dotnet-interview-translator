using InterviewTranslator.Shared.Models;

namespace InterviewTranslator.Shared.Contracts;

public interface ISubtitlePublisher
{
    Task PublishAsync(SubtitleItem item, CancellationToken cancellationToken);
}
