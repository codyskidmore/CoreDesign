namespace Sample.Api.WeatherForecasts.Update;

public interface IUpdateForecastHandler
{
    Task<OneOf<WeatherForecast, NotFoundMessage, BadRequestMessage>> UpdateAsync(
        Ulid id, Request request, Guid userId, CancellationToken ct);
}

public class UpdateForecastHandler(
    IReadRepository<SampleDbContext, WeatherForecast> readRepository,
    ICudRepository<SampleDbContext, WeatherForecast> cudRepository) : IUpdateForecastHandler
{
    public async Task<OneOf<WeatherForecast, NotFoundMessage, BadRequestMessage>> UpdateAsync(
        Ulid id, Request request, Guid userId, CancellationToken ct)
    {
        var forecast = await readRepository.GetAsync(f => f.Id == id, query => query, ct);
        if (forecast is null)
            return new NotFoundMessage($"Weather forecast with id {id} not found.");

        request.Apply(forecast);
        var success = await cudRepository.UpdateAsync(forecast, userId, ct);

        if (!success)
            return new BadRequestMessage("Failed to update weather forecast.");

        return forecast;
    }
}
