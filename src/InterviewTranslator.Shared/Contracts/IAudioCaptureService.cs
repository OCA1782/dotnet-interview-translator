using InterviewTranslator.Shared.Models;

namespace InterviewTranslator.Shared.Contracts;

public interface IAudioCaptureService
{
    IAsyncEnumerable<AudioFrame> CaptureAsync(CancellationToken cancellationToken);
    IReadOnlyList<AudioDeviceInfo> GetOutputDevices();
    void SetDevice(string deviceId);
}

public sealed class AudioDeviceInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}
