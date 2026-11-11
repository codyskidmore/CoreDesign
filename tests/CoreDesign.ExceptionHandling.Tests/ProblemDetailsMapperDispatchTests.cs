using CoreDesign.ExceptionHandling.Tests.Helpers;

namespace CoreDesign.ExceptionHandling.Tests;

public class ProblemDetailsMapperDispatchTests
{
    private readonly GeneratedProblemDetailsMapperStandIn _mapper = new();

    [Fact]
    public void TryMap_MostDerivedMapping_WinsOverBaseTypeMapping()
    {
        var mapped = _mapper.TryMap(new EntityNotFoundException("missing"), out var result);

        Assert.True(mapped);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Not found", result.Title);
    }

    [Fact]
    public void TryMap_BaseTypeMapping_MatchesInstanceWithNoOwnMapping()
    {
        var mapped = _mapper.TryMap(new DomainException("bad state"), out var result);

        Assert.True(mapped);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Domain rule violated", result.Title);
    }

    [Fact]
    public void TryMap_IncludeMessageFalse_DetailIsNullEvenThoughMessageIsSet()
    {
        var mapped = _mapper.TryMap(new SecretException("shh"), out var result);

        Assert.True(mapped);
        Assert.Null(result.Detail);
    }

    [Fact]
    public void TryMap_IncludeMessageTrue_DetailIsExceptionMessage()
    {
        var mapped = _mapper.TryMap(new EntityNotFoundException("missing"), out var result);

        Assert.True(mapped);
        Assert.Equal("missing", result.Detail);
    }

    [Fact]
    public void TryMap_MatchDerivedFalse_ExactTypeStillMatches()
    {
        var mapped = _mapper.TryMap(new ExactOnlyException("exact"), out var result);

        Assert.True(mapped);
        Assert.Equal(422, result.StatusCode);
    }

    [Fact]
    public void TryMap_MatchDerivedFalse_SubclassDoesNotMatch()
    {
        var mapped = _mapper.TryMap(new ExactOnlySubException("sub"), out _);

        Assert.False(mapped);
    }

    [Fact]
    public void TryMap_UnmappedExceptionType_ReturnsFalse()
    {
        var mapped = _mapper.TryMap(new InvalidOperationException("boom"), out _);

        Assert.False(mapped);
    }
}
