using InterviewTranslator.Shared.Models;
using InterviewTranslator.Subtitles;
using Xunit;

namespace InterviewTranslator.Tests;

public class SubtitleBufferTests
{
    [Fact]
    public void Add_SingleItem_GetRecentReturnsIt()
    {
        var buf = new SubtitleBuffer(5);
        var item = MakeItem("Hello", "Merhaba");
        buf.Add(item);

        var recent = buf.GetRecent();
        Assert.Single(recent);
        Assert.Equal("Merhaba", recent[0].TurkishText);
    }

    [Fact]
    public void Add_BeyondCapacity_OldestDropped()
    {
        var buf = new SubtitleBuffer(3);
        buf.Add(MakeItem("one", "bir"));
        buf.Add(MakeItem("two", "iki"));
        buf.Add(MakeItem("three", "üç"));
        buf.Add(MakeItem("four", "dört"));

        var recent = buf.GetRecent();
        Assert.Equal(3, recent.Count);
        Assert.Equal("iki", recent[0].TurkishText);
        Assert.Equal("dört", recent[2].TurkishText);
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var buf = new SubtitleBuffer(5);
        buf.Add(MakeItem("a", "a"));
        buf.Add(MakeItem("b", "b"));
        buf.Clear();

        Assert.Empty(buf.GetRecent());
    }

    [Fact]
    public void Changed_FiredOnAdd()
    {
        var buf = new SubtitleBuffer(5);
        int fired = 0;
        buf.Changed += () => fired++;

        buf.Add(MakeItem("x", "x"));
        buf.Add(MakeItem("y", "y"));

        Assert.Equal(2, fired);
    }

    [Fact]
    public void Changed_FiredOnClear()
    {
        var buf = new SubtitleBuffer(5);
        buf.Add(MakeItem("x", "x"));
        int fired = 0;
        buf.Changed += () => fired++;

        buf.Clear();
        Assert.Equal(1, fired);
    }

    [Fact]
    public void GetRecent_ThreadSafe_NoException()
    {
        var buf = new SubtitleBuffer(100);
        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(() =>
        {
            for (int j = 0; j < 50; j++)
                buf.Add(MakeItem($"en{j}", $"tr{j}"));
        }));
        Task.WaitAll(tasks.ToArray());
        Assert.True(buf.GetRecent().Count <= 100);
    }

    private static SubtitleItem MakeItem(string en, string tr) => new()
    {
        SegmentId = Guid.NewGuid(),
        EnglishText = en,
        TurkishText = tr,
        CreatedAt = DateTimeOffset.UtcNow,
        Latency = TimeSpan.FromMilliseconds(500)
    };
}
