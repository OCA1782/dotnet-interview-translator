using InterviewTranslator.Shared.Models;

namespace InterviewTranslator.Shared.Contracts;

public interface IMicrophoneCaptureService
{
    IAsyncEnumerable<AudioFrame> CaptureAsync(CancellationToken cancellationToken);
    IReadOnlyList<AudioDeviceInfo> GetInputDevices();
    void SetDevice(string deviceId);
}
