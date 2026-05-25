using InterviewTranslator.Shared.Options;
using Microsoft.Extensions.Options;

namespace InterviewTranslator.Desktop;

// DI'dan çıkarılmış AppOptions'a SettingsWindow'un statik erişimi için
public sealed class AppOptionsAccessor
{
    private static AppOptions? _current;
    public static AppOptions Current => _current ?? throw new InvalidOperationException("AppOptionsAccessor not initialized.");

    public static void Initialize(IOptions<AppOptions> options) => _current = options.Value;
}
