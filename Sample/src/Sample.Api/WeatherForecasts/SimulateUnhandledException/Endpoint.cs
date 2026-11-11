namespace Sample.Api.WeatherForecasts.SimulateUnhandledException;

public static class Endpoint
{
    public const string Name = nameof(SimulateUnhandledExceptionEndpoint);

    public static IEndpointRouteBuilder MapSimulateUnhandledException(this IEndpointRouteBuilder app)
    {
        app.MapGet(Paths.WeatherForecasts.SimulateUnhandledException, async (
                Ulid id,
                ISimulateUnhandledExceptionHandler handler,
                CancellationToken ct) =>
            {
                await handler.SimulateAsync(id, ct);
                return TypedResults.Ok();
            })
            .WithName(Name)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(Permissions.WeatherRead);

        return app;
    }

    private class SimulateUnhandledExceptionEndpoint;
}
