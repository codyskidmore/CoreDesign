using OneOf.Types;

namespace Sample.Api.WeatherForecasts.Delete;

[LoggingDecorator]
public interface IDeleteForecastHandler
{
    Task<OneOf<Success, NotFoundMessage>> DeleteAsync(Ulid id, Guid userId, CancellationToken ct);
}

public class DeleteForecastHandler(ICudRepository<SampleDbContext, WeatherForecast> repository) : IDeleteForecastHandler
{
    public async Task<OneOf<Success, NotFoundMessage>> DeleteAsync(Ulid id, Guid userId, CancellationToken ct)
    {
        var deleted = await repository.DeleteAsync(id, userId, ct);

        if (!deleted)
            return new NotFoundMessage($"Weather forecast with id {id} not found.");

        return new Success();
    }
}
