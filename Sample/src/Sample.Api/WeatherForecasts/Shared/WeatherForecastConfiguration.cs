namespace Sample.Api.WeatherForecasts.Shared;

public class WeatherForecastConfig : BaseEntityConfiguration<WeatherForecast>
{
    public override void Configure(EntityTypeBuilder<WeatherForecast> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.Location)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Summary)
            .HasMaxLength(500);

        builder.HasIndex(e => new { e.Location, e.Date })
            .IsUnique();

        builder.ToTable("WeatherForecasts", Schemas.Sample.ToString());
    }
}
