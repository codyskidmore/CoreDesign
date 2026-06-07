using CoreDesign.SeedTool;

namespace CoreDesign.SeedTool.Tests;

public class ContextLoaderTests
{
    [Fact]
    public void Load_ThrowsInvalidOperation_WhenNoFactoryFound()
    {
        // Use the test assembly itself, which has no ICoreDesignSeedFactory<T> implementation.
        var assemblyPath = typeof(ContextLoaderTests).Assembly.Location;

        var ex = Assert.Throws<InvalidOperationException>(
            () => ContextLoader.Load(assemblyPath, "dummy"));

        Assert.Contains("ICoreDesignSeedFactory", ex.Message);
        Assert.Contains("CoreDesignSeedFactory<TContext>", ex.Message);
    }

    [Fact]
    public void Load_ErrorMessage_IncludesAssemblyName()
    {
        var assemblyPath = typeof(ContextLoaderTests).Assembly.Location;

        var ex = Assert.Throws<InvalidOperationException>(
            () => ContextLoader.Load(assemblyPath, "dummy"));

        Assert.Contains("CoreDesign.SeedTool.Tests", ex.Message);
    }
}
