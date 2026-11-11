using CoreDesign.Shared.Infrastructure;

namespace CoreDesign.Shared.Tests.Infrastructure;

public class UnionTests
{
    private record Cabin(string Name);

    [Fact]
    public void Union2_ConvertsImplicitlyFromEachCase()
    {
        Union<Cabin, NotFoundMessage> found = new Cabin("Loon");
        Union<Cabin, NotFoundMessage> missing = new NotFoundMessage("missing");

        Assert.True(found is Cabin);
        Assert.True(missing is NotFoundMessage);
    }

    [Fact]
    public void Union2_ConvertsFromDerivedTypeToInterfaceCase()
    {
        Union<IEnumerable<Cabin>, NotFoundMessage> result = new List<Cabin> { new("Loon"), new("Heron") };

        Assert.True(result is IEnumerable<Cabin> { } cabins && cabins.Count() == 2);
    }

    [Fact]
    public void Union2_Match_RunsFunctionForHeldCase()
    {
        Union<Cabin, NotFoundMessage> found = new Cabin("Loon");
        Union<Cabin, NotFoundMessage> missing = new NotFoundMessage("missing");

        Assert.Equal("Loon", found.Match(c => c.Name, nf => nf.Message));
        Assert.Equal("missing", missing.Match(c => c.Name, nf => nf.Message));
    }

    [Fact]
    public void Union3_Match_RunsFunctionForHeldCase()
    {
        Union<Cabin, NotFoundMessage, BadRequestMessage> bad = new BadRequestMessage("bad");

        Assert.Equal("bad", bad.Match(c => c.Name, nf => nf.Message, br => br.Message));
    }

    [Fact]
    public void Union4_Match_RunsFunctionForHeldCase()
    {
        Union<Success, NotFoundMessage, BadRequestMessage, ErrorMessage> error = new ErrorMessage("boom");

        Assert.Equal("boom", error.Match(_ => "ok", nf => nf.Message, br => br.Message, e => e.Message));
    }

    [Fact]
    public void Union_SwitchExpression_IsExhaustiveWithoutDiscard()
    {
        Union<Cabin, NotFoundMessage> found = new Cabin("Loon");

        var text = found switch
        {
            Cabin c => c.Name,
            NotFoundMessage nf => nf.Message,
            null => "empty",
        };

        Assert.Equal("Loon", text);
    }

    [Fact]
    public void Union_Default_HoldsNoValue()
    {
        Union<Cabin, NotFoundMessage> empty = default;

        Assert.Null(empty.Value);
        Assert.Throws<InvalidOperationException>(() => empty.Match(c => c.Name, nf => nf.Message));
    }

    [Fact]
    public void Union_ImplementsIUnion()
    {
        Union<Cabin, NotFoundMessage> found = new Cabin("Loon");

        Assert.IsAssignableFrom<System.Runtime.CompilerServices.IUnion>(found);
    }
}
