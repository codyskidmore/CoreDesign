namespace Sample.Api.WeatherForecasts.GetAll;

public interface IGetAllForecastsHandler
{
    Task<OneOf<List<WeatherForecast>, NotFoundMessage>> GetAllAsync(CancellationToken ct);
}

public class GetAllForecastsHandler(IReadRepository<SampleDbContext, WeatherForecast> repository) : IGetAllForecastsHandler
{
    public async Task<OneOf<List<WeatherForecast>, NotFoundMessage>> GetAllAsync(CancellationToken ct)
    {
        var forecasts = await repository.GetAllAsync(ct);

        if (!forecasts.Any())
            return new NotFoundMessage("No weather forecasts found.");

        return forecasts.ToList();
    }
}
