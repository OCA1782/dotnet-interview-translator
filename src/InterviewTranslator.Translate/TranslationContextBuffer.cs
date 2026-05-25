namespace InterviewTranslator.Translate;

// Son N segmentin İngilizce metnini bağlam olarak tutar
// LibreTranslate'e önceki cümleleri prefix olarak göndermek çeviride
// bağlamsal tutarlılığı artırır (özellikle teknik sorularda).
public sealed class TranslationContextBuffer
{
    private readonly int _capacity;
    private readonly Queue<string> _history = new();
    private string? _lastText;

    public TranslationContextBuffer(int capacity = 3)
    {
        _capacity = capacity;
    }

    public bool IsDuplicate(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        // Levenshtein benzerliği %90+ ise duplicate say
        if (_lastText is not null && Similarity(text, _lastText) > 0.90)
            return true;
        return false;
    }

    public string BuildContextualText(string text)
    {
        // Bağlamı prefix olarak ekle — LibreTranslate cümle sıralamasını kullanır
        var context = string.Join(" ", _history);
        return string.IsNullOrEmpty(context) ? text : $"{context} {text}";
    }

    public void Add(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _lastText = text;
        _history.Enqueue(text);
        if (_history.Count > _capacity)
            _history.Dequeue();
    }

    public void Clear()
    {
        _history.Clear();
        _lastText = null;
    }

    // Jaccard token benzerliği — basit ama etkili duplicate tespiti için yeterli
    private static double Similarity(string a, string b)
    {
        var setA = new HashSet<string>(a.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var setB = new HashSet<string>(b.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (setA.Count == 0 && setB.Count == 0) return 1.0;
        int intersect = setA.Intersect(setB).Count();
        int union     = setA.Union(setB).Count();
        return union == 0 ? 0 : (double)intersect / union;
    }
}
