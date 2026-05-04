namespace SampleApi.Api.WeatherForecasts.Services;

public class WeatherForecastService(
    ICudRepository<SampleApiDbContext, WeatherForecast> cudRepository,
    IReadRepository<SampleApiDbContext, WeatherForecast> readRepository,
    ILogger<WeatherForecastService> logger
) : IWeatherForecastService
{
    public async Task<OneOf<WeatherForecast, ErrorMessage>> CreateAsync(Guid userId, WeatherForecastRequest request, CancellationToken ct)
    {
        var forecast = request.ToNewEntity(userId);

        var success = await cudRepository.InsertAsync(forecast, userId, ct);
        if (!success)
            return new ErrorMessage("Failed to create weather forecast.");

        return forecast;
    }

    public async Task<OneOf<bool, NotFoundMessage>> DeleteAsync(Ulid id, Guid userId, CancellationToken ct)
    {
        var notDeleted = !await cudRepository.DeleteAsync(id, userId, ct);
        if (notDeleted)
            return new NotFoundMessage($"Weather forecast with id {id} not found for deletion.");

        return true;
    }

    public async Task<OneOf<IEnumerable<WeatherForecast>, NotFoundMessage>> GetAllAsync(Guid userId, CancellationToken ct)
    {
        var forecasts = await readRepository.GetAllAsync(ct);
        if (!forecasts.Any())
            return new NotFoundMessage("No weather forecasts found.");

        return forecasts.ToList();
    }

    public async Task<OneOf<WeatherForecast, NotFoundMessage>> GetAsync(Ulid id, Guid userId, CancellationToken ct)
    {
        var forecast = await readRepository.GetAsync(f => f.Id == id, query => query, ct);

        if (forecast is null)
            return new NotFoundMessage($"Weather forecast not found for id: {id}");

        return forecast;
    }

    public async Task<OneOf<WeatherForecast, NotFoundMessage, BadRequestMessage>> UpdateAsync(Ulid id, Guid userId, WeatherForecastRequest request, CancellationToken ct)
    {
        var forecast = await readRepository.GetAsync(f => f.Id == id, query => query, ct);
        if (forecast is null)
            return new NotFoundMessage($"Weather forecast with id {id} not found.");

        var success = await cudRepository.UpdateAsync(forecast.UpdateFrom(request), userId, ct);
        if (!success)
            return new BadRequestMessage("Failed to update weather forecast.");

        return forecast;
    }
}
