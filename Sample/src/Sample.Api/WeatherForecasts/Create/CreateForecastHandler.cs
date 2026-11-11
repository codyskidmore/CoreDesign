namespace Sample.Api.WeatherForecasts.Create;

[LoggingDecorator]
public interface ICreateForecastHandler
{
    Task<Union<WeatherForecast, BadRequestMessage>> CreateAsync(Request request, CancellationToken ct);
}

public class CreateForecastHandler(ICudRepository<SampleDbContext, WeatherForecast> repository) : ICreateForecastHandler
{
    public async Task<Union<WeatherForecast, BadRequestMessage>> CreateAsync(Request request, CancellationToken ct)
    {
        var entity = request.ToNewEntity();
        var success = await repository.InsertAsync(entity, ct);

        if (!success)
            return new BadRequestMessage("Failed to create weather forecast.");

        return entity;
    }
}
