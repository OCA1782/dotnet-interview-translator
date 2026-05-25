using InterviewTranslator.Shared.Contracts;
using InterviewTranslator.Subtitles;
using Xunit;

namespace InterviewTranslator.Tests;

public class SuggestionBufferTests
{
    [Fact]
    public void Add_SingleItem_GetRecentReturnsIt()
    {
        var buf = new SuggestionBuffer(3);
        buf.Add(MakeItem("konuşma", "suggested answer"));

        var recent = buf.GetRecent();
        Assert.Single(recent);
        Assert.Equal("konuşma", recent[0].TurkishText);
        Assert.Equal("suggested answer", recent[0].EnglishSuggestion);
    }

    [Fact]
    public void Add_BeyondCapacity_OldestDropped()
    {
        var buf = new SuggestionBuffer(3);
        buf.Add(MakeItem("bir", "one"));
        buf.Add(MakeItem("iki", "two"));
        buf.Add(MakeItem("üç", "three"));
        buf.Add(MakeItem("dört", "four"));

        var recent = buf.GetRecent();
        Assert.Equal(3, recent.Count);
        Assert.Equal("iki", recent[0].TurkishText);
        Assert.Equal("dört", recent[2].TurkishText);
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var buf = new SuggestionBuffer(3);
        buf.Add(MakeItem("a", "a"));
        buf.Add(MakeItem("b", "b"));
        buf.Clear();

        Assert.Empty(buf.GetRecent());
    }

    [Fact]
    public void Changed_FiredOnAdd()
    {
        var buf = new SuggestionBuffer(3);
        int fired = 0;
        buf.Changed += () => fired++;

        buf.Add(MakeItem("x", "x"));
        buf.Add(MakeItem("y", "y"));

        Assert.Equal(2, fired);
    }

    [Fact]
    public void Changed_FiredOnClear()
    {
        var buf = new SuggestionBuffer(3);
        buf.Add(MakeItem("x", "x"));
        int fired = 0;
        buf.Changed += () => fired++;

        buf.Clear();
        Assert.Equal(1, fired);
    }

    [Fact]
    public void GetRecent_ThreadSafe_NoException()
    {
        var buf = new SuggestionBuffer(100);
        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(() =>
        {
            for (int j = 0; j < 50; j++)
                buf.Add(MakeItem($"tr{j}", $"en{j}"));
        }));
        Task.WaitAll(tasks.ToArray());
        Assert.True(buf.GetRecent().Count <= 100);
    }

    [Fact]
    public void GetRecent_EmptyBuffer_ReturnsEmpty()
    {
        var buf = new SuggestionBuffer(5);
        Assert.Empty(buf.GetRecent());
    }

    private static SuggestionItem MakeItem(string tr, string en) => new()
    {
        SegmentId         = Guid.NewGuid(),
        TurkishText       = tr,
        EnglishSuggestion = en,
        CreatedAt         = DateTimeOffset.UtcNow
    };
}
