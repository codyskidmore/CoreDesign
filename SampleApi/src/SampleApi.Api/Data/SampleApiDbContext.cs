namespace SampleApi.Api.Data;

public class SampleApiDbContext(DbContextOptions<SampleApiDbContext> options) : DbContext(options)
{
    public DbSet<WeatherForecast> WeatherForecasts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(nameof(Schemas.SampleApi));

        modelBuilder.ApplyConfiguration(new WeatherForecastConfig());
    }
}
