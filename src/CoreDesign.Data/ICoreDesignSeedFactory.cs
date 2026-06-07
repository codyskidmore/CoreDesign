namespace CoreDesign.Data;

/// <summary>
/// Contract a DbContext factory must implement so <c>dotnet seed</c> can instantiate
/// the context without a running host or DI container.
/// </summary>
public interface ICoreDesignSeedFactory<TContext> where TContext : DbContext
{
    /// <summary>Creates and returns a <typeparamref name="TContext"/> for the given connection string.</summary>
    TContext CreateContext(string connectionString);
}

/// <summary>
/// Base class for <see cref="ICoreDesignSeedFactory{TContext}"/> implementations.
/// Override <see cref="Configure"/> to register the EF Core provider and
/// <see cref="Create"/> to instantiate the context.
/// </summary>
public abstract class CoreDesignSeedFactory<TContext> : ICoreDesignSeedFactory<TContext>
    where TContext : DbContext
{
    /// <inheritdoc/>
    public TContext CreateContext(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        Configure(builder, connectionString);
        return Create(builder.Options);
    }

    /// <summary>Registers the EF Core provider and any other options on <paramref name="builder"/>.</summary>
    protected abstract void Configure(DbContextOptionsBuilder<TContext> builder, string connectionString);

    /// <summary>Constructs and returns a <typeparamref name="TContext"/> from the built options.</summary>
    protected abstract TContext Create(DbContextOptions<TContext> options);
}
