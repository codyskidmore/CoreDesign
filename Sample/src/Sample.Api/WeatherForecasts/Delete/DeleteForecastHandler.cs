namespace Sample.Api.WeatherForecasts.Delete;

[LoggingDecorator]
public interface IDeleteForecastHandler
{
    Task<Union<Success, NotFoundMessage>> DeleteAsync(Ulid id, CancellationToken ct);
}

public class DeleteForecastHandler(ICudRepository<SampleDbContext, WeatherForecast> repository) : IDeleteForecastHandler
{
    public async Task<Union<Success, NotFoundMessage>> DeleteAsync(Ulid id, CancellationToken ct)
    {
        var deleted = await repository.SoftDeleteAsync(id, ct);

        if (!deleted)
            return new NotFoundMessage($"Weather forecast with id {id} not found.");

        return new Success();
    }
}
