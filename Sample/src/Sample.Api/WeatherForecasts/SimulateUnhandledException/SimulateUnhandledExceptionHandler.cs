namespace Sample.Api.WeatherForecasts.SimulateUnhandledException;

[LoggingDecorator]
public interface ISimulateUnhandledExceptionHandler
{
    Task SimulateAsync(Ulid id, CancellationToken ct);
}

public class SimulateUnhandledExceptionHandler : ISimulateUnhandledExceptionHandler
{
    // Do not follow this model for your design. This endpoint exists solely to demonstrate
    // CoreDesign.ExceptionHandling's zero-config fallback: an exception nobody mapped and
    // nobody expected. Ordinary error conditions (not found, validation, etc.) should return
    // an error via OneOf<T, ...>, as the GetById slice does, not be represented as a thrown
    // exception.
    public Task SimulateAsync(Ulid id, CancellationToken ct)
    {
        throw new InvalidOperationException(
            $"Simulated unhandled exception for weather forecast '{id}', demonstrating CoreDesign.ExceptionHandling's zero-config fallback response.");
    }
}
