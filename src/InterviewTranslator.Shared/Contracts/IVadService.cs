using InterviewTranslator.Shared.Models;

namespace InterviewTranslator.Shared.Contracts;

public interface IVadService
{
    IAsyncEnumerable<SpeechSegment> DetectSpeechAsync(
        IAsyncEnumerable<AudioFrame> frames,
        CancellationToken cancellationToken);
}
