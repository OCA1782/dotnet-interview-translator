using InterviewTranslator.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewTranslator.Stt;

public static class SttExtensions
{
    public static IServiceCollection AddStt(this IServiceCollection services)
    {
        services.AddSingleton<ISttEngine, WhisperCppSttEngine>();
        // Concrete type forward — SettingsWindow ve diğerleri doğrudan çözebilsin
        services.AddSingleton<WhisperCppSttEngine>(sp =>
            (WhisperCppSttEngine)sp.GetRequiredService<ISttEngine>());
        services.AddSingleton<MicWhisperSttEngine>();
        return services;
    }
}
