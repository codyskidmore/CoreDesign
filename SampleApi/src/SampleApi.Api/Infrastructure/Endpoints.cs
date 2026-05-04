namespace SampleApi.Api.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        app.AddWeatherForecastsModule();

        return app;
    }
}
