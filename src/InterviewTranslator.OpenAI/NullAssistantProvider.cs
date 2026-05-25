using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Shared.Models;

namespace InterviewTranslator.OpenAI;

/// OpenAI devre dışıyken kullanılır — hiçbir şey yapmaz.
public sealed class NullAssistantProvider : IInterviewAssistantProvider
{
    public Task<InterviewAssistResult> AnalyzeQuestionAsync(
        InterviewAssistRequest request,
        CancellationToken cancellationToken)
        => Task.FromResult(InterviewAssistResult.Empty);
}
