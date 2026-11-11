using CoreDesign.Data.Infrastructure;
using CoreDesign.Data.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace CoreDesign.Data.Tests.Infrastructure;

public class UtcDateTimeConventionTests
{
    [Fact]
    public async Task SavingChanges_NormalizesBaseEntityAuditDates_ToUtc()
    {
        await using var ctx = DbContextFactory.CreateContext();
        var entity = new TestEntity
        {
            Name = "Base",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local)
        };

        ctx.TestEntities.Add(entity);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var loaded = await ctx.TestEntities.IgnoreQueryFilters().FirstAsync(e => e.Id == entity.Id);

        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, loaded.UpdatedAt.Kind);
    }

    [Fact]
    public async Task SavingChanges_NormalizesCustomEntityDateTimeProperty_ToUtc()
    {
        // Proves the convention applies to every DateTime property in the model, not just
        // BaseEntity's CreatedAt/UpdatedAt: this entity's own EventAt was never opted in
        // individually.
        await using var ctx = DbContextFactory.CreateContext();
        var entity = new TestEntityWithCustomDates
        {
            Name = "Custom",
            EventAt = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Unspecified)
        };

        ctx.TestEntitiesWithCustomDates.Add(entity);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var loaded = await ctx.TestEntitiesWithCustomDates.IgnoreQueryFilters().FirstAsync(e => e.Id == entity.Id);

        Assert.Equal(DateTimeKind.Utc, loaded.EventAt.Kind);
    }

    [Fact]
    public async Task SavingChanges_NormalizesNullableCustomDateTimeProperty_WhenSet()
    {
        await using var ctx = DbContextFactory.CreateContext();
        var entity = new TestEntityWithCustomDates
        {
            Name = "Nullable",
            EventAt = DateTime.UtcNow,
            ReminderAt = new DateTime(2026, 6, 30, 8, 0, 0, DateTimeKind.Local)
        };

        ctx.TestEntitiesWithCustomDates.Add(entity);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var loaded = await ctx.TestEntitiesWithCustomDates.IgnoreQueryFilters().FirstAsync(e => e.Id == entity.Id);

        Assert.NotNull(loaded.ReminderAt);
        Assert.Equal(DateTimeKind.Utc, loaded.ReminderAt!.Value.Kind);
    }

    [Fact]
    public async Task SavingChanges_LeavesNullNullableDateTimeProperty_Null()
    {
        await using var ctx = DbContextFactory.CreateContext();
        var entity = new TestEntityWithCustomDates
        {
            Name = "NullReminder",
            EventAt = DateTime.UtcNow,
            ReminderAt = null
        };

        ctx.TestEntitiesWithCustomDates.Add(entity);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var loaded = await ctx.TestEntitiesWithCustomDates.IgnoreQueryFilters().FirstAsync(e => e.Id == entity.Id);

        Assert.Null(loaded.ReminderAt);
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void UtcDateTimeConverter_ConvertToProvider_AlwaysProducesUtcKind(DateTimeKind kind)
    {
        var converter = new UtcDateTimeConverter();
        var input = new DateTime(2026, 6, 30, 12, 0, 0, kind);

        var result = (DateTime)converter.ConvertToProvider(input)!;

        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void UtcNullableDateTimeConverter_ConvertToProvider_NullPassesThrough()
    {
        var converter = new UtcNullableDateTimeConverter();

        var result = converter.ConvertToProvider(null);

        Assert.Null(result);
    }
}
