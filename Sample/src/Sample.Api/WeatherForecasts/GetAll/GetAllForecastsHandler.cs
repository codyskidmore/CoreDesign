namespace Sample.Api.WeatherForecasts.GetAll;

[LoggingDecorator]
public interface IGetAllForecastsHandler
{
    Task<Union<List<WeatherForecast>, NotFoundMessage>> GetAllAsync(CancellationToken ct);
}

public class GetAllForecastsHandler(IReadRepository<SampleDbContext, WeatherForecast> repository) : IGetAllForecastsHandler
{
    public async Task<Union<List<WeatherForecast>, NotFoundMessage>> GetAllAsync(CancellationToken ct)
    {
        var forecasts = await repository.GetAllAsync(ct);

        if (!forecasts.Any())
            return new NotFoundMessage("No weather forecasts found.");

        return forecasts.ToList();
    }
}
