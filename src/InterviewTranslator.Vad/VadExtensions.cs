using InterviewTranslator.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewTranslator.Vad;

public static class VadExtensions
{
    public static IServiceCollection AddVad(this IServiceCollection services)
    {
        services.AddSingleton<IVadService, EnergyBasedVadService>();
        return services;
    }
}
