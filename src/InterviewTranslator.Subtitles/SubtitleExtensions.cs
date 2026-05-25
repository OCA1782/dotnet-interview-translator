using InterviewTranslator.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewTranslator.Subtitles;

public static class SubtitleExtensions
{
    public static IServiceCollection AddSubtitles(this IServiceCollection services)
    {
        services.AddSingleton<SubtitleBuffer>();
        services.AddSingleton<ISubtitlePublisher, SubtitlePublisher>();
        services.AddSingleton<SuggestionBuffer>();
        services.AddSingleton<ISuggestionPublisher, SuggestionPublisher>();
        return services;
    }
}
