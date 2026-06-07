using CoreDesign.Data.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace CoreDesign.Data.Tests.Helpers;

public class TestEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
}

public class TestDbContext : CoreDesignDbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options) { }

    public DbSet<TestEntity> TestEntities => Set<TestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TestEntityConfiguration());
    }
}

public class TestEntityConfiguration : BaseEntityConfiguration<TestEntity>
{
    public override void Configure(EntityTypeBuilder<TestEntity> builder)
    {
        base.Configure(builder);
        builder.Property(e => e.Name).IsRequired();
    }
}

public class TestUserAccessor(Guid userId) : ICurrentUserAccessor
{
    public Guid UserId => userId;
}

public static class DbContextFactory
{
    public static TestDbContext CreateContext(string? dbName = null, Guid? userId = null)
    {
        var targetUserId = userId ?? Guid.Empty;

        var appServices = new ServiceCollection();
        appServices.AddSingleton<ICurrentUserAccessor>(new TestUserAccessor(targetUserId));
        var appSp = appServices.BuildServiceProvider();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .UseApplicationServiceProvider(appSp)
            .AddInterceptors(new AuditInterceptor())
            .Options;

        return new TestDbContext(options);
    }
}
