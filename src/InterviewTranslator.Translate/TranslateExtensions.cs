using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Shared.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InterviewTranslator.Translate;

public static class TranslateExtensions
{
    public static IServiceCollection AddTranslation(this IServiceCollection services)
    {
        services.AddSingleton<GlossaryService>();
        services.AddHttpClient<ITranslationService, LibreTranslateService>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<AppOptions>>().Value.Translation;
            client.BaseAddress = new Uri(opts.LibreTranslateUrl);
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        });
        return services;
    }
}
