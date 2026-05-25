using InterviewTranslator.Shared.Models;

namespace InterviewTranslator.Shared.Contracts;

public interface ITranslationService
{
    Task<TranslationResult> TranslateAsync(TranscriptionResult transcript, CancellationToken cancellationToken);
}
