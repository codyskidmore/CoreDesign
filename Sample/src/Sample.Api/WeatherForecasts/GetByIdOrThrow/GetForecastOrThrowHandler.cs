namespace Sample.Api.WeatherForecasts.GetByIdOrThrow;

[LoggingDecorator]
public interface IGetForecastOrThrowHandler
{
    Task<WeatherForecast> GetByIdAsync(Ulid id, CancellationToken ct);
}

public class GetForecastOrThrowHandler(IReadRepository<SampleDbContext, WeatherForecast> repository) : IGetForecastOrThrowHandler
{
    public async Task<WeatherForecast> GetByIdAsync(Ulid id, CancellationToken ct)
    {
        var forecast = await repository.GetAsync(f => f.Id == id, query => query, ct);

        return forecast ?? throw new WeatherForecastNotFoundException(id);
    }
}
