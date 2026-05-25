namespace InterviewTranslator.Shared.Contracts;

public interface ITtsService
{
    void Speak(string text);
    void Stop();
}
