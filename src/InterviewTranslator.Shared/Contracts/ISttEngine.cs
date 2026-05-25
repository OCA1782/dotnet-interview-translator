using InterviewTranslator.Shared.Models;

namespace InterviewTranslator.Shared.Contracts;

public interface ISttEngine
{
    Task<TranscriptionResult> TranscribeAsync(SpeechSegment segment, CancellationToken cancellationToken);
}
