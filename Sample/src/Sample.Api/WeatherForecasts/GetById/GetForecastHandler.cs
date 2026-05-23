namespace Sample.Api.WeatherForecasts.GetById;

[LoggingDecorator]
public interface IGetForecastHandler
{
    Task<OneOf<WeatherForecast, NotFoundMessage>> GetByIdAsync(Ulid id, CancellationToken ct);
}

public class GetForecastHandler(IReadRepository<SampleDbContext, WeatherForecast> repository) : IGetForecastHandler
{
    public async Task<OneOf<WeatherForecast, NotFoundMessage>> GetByIdAsync(Ulid id, CancellationToken ct)
    {
        var forecast = await repository.GetAsync(f => f.Id == id, query => query, ct);

        if (forecast is null)
            return new NotFoundMessage($"Weather forecast not found for id: {id}");

        return forecast;
    }
}
