public interface IWeatherForecastService
{
    Task<OneOf<WeatherForecast, ErrorMessage>> CreateAsync(
        Guid userId,
        WeatherForecastRequest request,
        CancellationToken ct);

    Task<OneOf<bool, NotFoundMessage>> DeleteAsync(
        Ulid id,
        Guid userId,
        CancellationToken ct);

    Task<OneOf<IEnumerable<WeatherForecast>, NotFoundMessage>> GetAllAsync(
        Guid userId,
        CancellationToken ct);

    Task<OneOf<WeatherForecast, NotFoundMessage>> GetAsync(
        Ulid id,
        Guid userId,
        CancellationToken ct);

    Task<OneOf<WeatherForecast, NotFoundMessage, BadRequestMessage>> UpdateAsync(
        Ulid id,
        Guid userId,
        WeatherForecastRequest request,
        CancellationToken ct);
}
