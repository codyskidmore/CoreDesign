using CoreDesign.Data.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoreDesign.Data.Tests.Helpers;

public class TestEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
}

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

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

public static class DbContextFactory
{
    public static DbContextOptions<TestDbContext> CreateOptions(string? dbName = null) =>
        new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
}
