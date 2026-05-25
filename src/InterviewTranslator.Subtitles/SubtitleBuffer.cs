using InterviewTranslator.Shared.Models;

namespace InterviewTranslator.Subtitles;

public sealed class SubtitleBuffer
{
    private readonly int _maxItems;
    private readonly List<SubtitleItem> _items = new();
    private readonly object _lock = new();

    public event Action? Changed;

    public SubtitleBuffer(int maxItems = 200) => _maxItems = maxItems;

    /// Yeni segment ekler; aynı SegmentId varsa günceller (iki aşamalı publish için).
    public void AddOrUpdate(SubtitleItem item)
    {
        lock (_lock)
        {
            var idx = _items.FindIndex(i => i.SegmentId == item.SegmentId);
            if (idx >= 0)
                _items[idx] = item;
            else
            {
                _items.Add(item);
                if (_items.Count > _maxItems)
                    _items.RemoveAt(0);
            }
        }
        Changed?.Invoke();
    }

    public IReadOnlyList<SubtitleItem> GetRecent()
    {
        lock (_lock) return _items.AsReadOnly();
    }

    public void Clear()
    {
        lock (_lock) _items.Clear();
        Changed?.Invoke();
    }
}
