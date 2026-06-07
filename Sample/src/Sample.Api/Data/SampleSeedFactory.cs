using CoreDesign.Data;
using Microsoft.EntityFrameworkCore;

namespace Sample.Api.Data;

public class SampleSeedFactory : CoreDesignSeedFactory<SampleDbContext>
{
    protected override void Configure(DbContextOptionsBuilder<SampleDbContext> builder, string connectionString)
        => builder.UseSqlServer(connectionString);

    protected override SampleDbContext Create(DbContextOptions<SampleDbContext> options)
        => new(options);
}
