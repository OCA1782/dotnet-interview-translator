using InterviewTranslator.Shared.Contracts;

namespace InterviewTranslator.Subtitles;

public sealed class SuggestionBuffer
{
    private readonly int _maxItems;
    private readonly List<SuggestionItem> _items = new();
    private readonly object _lock = new();

    public event Action? Changed;

    public SuggestionBuffer(int maxItems = 10) => _maxItems = maxItems;

    public void Add(SuggestionItem item)
    {
        lock (_lock)
        {
            _items.Add(item);
            if (_items.Count > _maxItems)
                _items.RemoveAt(0);
        }
        Changed?.Invoke();
    }

    public IReadOnlyList<SuggestionItem> GetRecent()
    {
        lock (_lock)
            return _items.AsReadOnly();
    }

    public void Clear()
    {
        lock (_lock)
            _items.Clear();
        Changed?.Invoke();
    }
}
