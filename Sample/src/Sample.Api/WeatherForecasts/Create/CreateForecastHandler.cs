namespace Sample.Api.WeatherForecasts.Create;

[LoggingDecorator]
public interface ICreateForecastHandler
{
    Task<OneOf<WeatherForecast, BadRequestMessage>> CreateAsync(Request request, Guid userId, CancellationToken ct);
}

public class CreateForecastHandler(ICudRepository<SampleDbContext, WeatherForecast> repository) : ICreateForecastHandler
{
    public async Task<OneOf<WeatherForecast, BadRequestMessage>> CreateAsync(
        Request request, Guid userId, CancellationToken ct)
    {
        var entity = request.ToNewEntity();
        var success = await repository.InsertAsync(entity, userId, ct);

        if (!success)
            return new BadRequestMessage("Failed to create weather forecast.");

        return entity;
    }
}
