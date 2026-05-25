using InterviewTranslator.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewTranslator.Audio;

public static class AudioExtensions
{
    public static IServiceCollection AddAudioCapture(this IServiceCollection services)
    {
        services.AddSingleton<IAudioCaptureService, WasapiLoopbackCaptureService>();
        services.AddSingleton<IMicrophoneCaptureService, MicrophoneCaptureService>();
        return services;
    }
}
