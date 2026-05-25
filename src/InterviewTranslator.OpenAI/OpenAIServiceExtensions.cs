using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Shared.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InterviewTranslator.OpenAI;

public static class OpenAIServiceExtensions
{
    public static IServiceCollection AddOpenAIAssistant(this IServiceCollection services)
    {
        services.AddHttpClient("openai", (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<AppOptions>>().Value.OpenAI;
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {opts.ApiKey}");
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds + 2);
        });

        services.AddSingleton<OpenAIAssistantProvider>();
        services.AddSingleton<NullAssistantProvider>();

        services.AddSingleton<IInterviewAssistantProvider>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<AppOptions>>().Value.OpenAI;
            if (opts.Mode != OpenAIMode.Disabled && !string.IsNullOrWhiteSpace(opts.ApiKey))
                return sp.GetRequiredService<OpenAIAssistantProvider>();
            return sp.GetRequiredService<NullAssistantProvider>();
        });

        return services;
    }
}
