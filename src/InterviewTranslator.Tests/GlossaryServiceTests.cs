using InterviewTranslator.Translate;
using Xunit;

namespace InterviewTranslator.Tests;

public class GlossaryServiceTests
{
    private readonly GlossaryService _sut = new();

    [Fact]
    public void Protect_ReplacesKnownTerm_WithPlaceholder()
    {
        var result = _sut.Protect("We use microservice architecture.");
        Assert.DoesNotContain("microservice", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("__TERM", result);
    }

    [Fact]
    public void Restore_AfterProtect_RestoresOriginalTerm()
    {
        var original = "The deployment pipeline uses Docker containers.";
        var protected_ = _sut.Protect(original);
        var restored = _sut.Restore(protected_);

        Assert.Contains("deployment", restored, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Docker", restored, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("__TERM", restored);
    }

    [Fact]
    public void Protect_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", _sut.Protect(""));
    }

    [Fact]
    public void Protect_NoKnownTerms_ReturnsSameText()
    {
        var text = "Hello how are you today?";
        Assert.Equal(text, _sut.Protect(text));
    }

    [Fact]
    public void Restore_WithoutProtect_ReturnsSameText()
    {
        var text = "some translated text";
        Assert.Equal(text, _sut.Restore(text));
    }

    [Fact]
    public void Protect_IsCaseInsensitive()
    {
        var lower = _sut.Protect("using kubernetes for orchestration");
        var upper = _sut.Protect("using KUBERNETES for orchestration");
        // Her iki durumda da placeholder içermeli
        Assert.Contains("__TERM", lower);
        Assert.Contains("__TERM", upper);
    }

    [Theory]
    [InlineData("We run CI/CD pipelines on every commit.")]
    [InlineData("The load balancer distributes traffic.")]
    [InlineData("Our API uses REST architecture.")]
    public void Protect_ThenRestore_PreservesTerms(string input)
    {
        var protected_ = _sut.Protect(input);
        var restored   = _sut.Restore(protected_);
        Assert.DoesNotContain("__TERM", restored);
    }
}
