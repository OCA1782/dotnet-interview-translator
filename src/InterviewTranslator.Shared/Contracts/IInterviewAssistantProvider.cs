using InterviewTranslator.Shared.Models;

namespace InterviewTranslator.Shared.Contracts;

public interface IInterviewAssistantProvider
{
    Task<InterviewAssistResult> AnalyzeQuestionAsync(
        InterviewAssistRequest request,
        CancellationToken cancellationToken);
}
