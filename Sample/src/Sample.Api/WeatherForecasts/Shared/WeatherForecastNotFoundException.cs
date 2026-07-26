namespace Sample.Api.WeatherForecasts.Shared;

// Demonstrates CoreDesign.ExceptionHandling: mark a domain exception with [ProblemMapping] and
// throwing it anywhere in the pipeline produces a consistent RFC 7807 response with no per-endpoint
// try/catch or .Match() call, unlike the OneOf<WeatherForecast, NotFoundMessage> approach used by
// the GetById feature.
[ProblemMapping(404, Title = "Weather forecast not found")]
public sealed class WeatherForecastNotFoundException(Ulid id)
    : Exception($"Weather forecast '{id}' was not found.");
