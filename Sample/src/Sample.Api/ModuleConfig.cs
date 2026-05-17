using Sample.Api.WeatherForecasts.Create;
using Sample.Api.WeatherForecasts.Delete;
using Sample.Api.WeatherForecasts.GetAll;
using Sample.Api.WeatherForecasts.GetById;
using Sample.Api.WeatherForecasts.Update;

namespace Sample.Api;

public static class ModuleConfig
{
    public static IHostApplicationBuilder AddWeatherForecastsModule(this IHostApplicationBuilder builder)
    {
        var s = builder.Services;

        s.AddTransient<IReadRepository<SampleDbContext, WeatherForecast>, ReadRepository<SampleDbContext, WeatherForecast>>();
        s.AddTransient<ICudRepository<SampleDbContext, WeatherForecast>, CudRepository<SampleDbContext, WeatherForecast>>();

return builder;
    }

    public static IEndpointRouteBuilder AddWeatherForecastsModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Paths.WeatherForecasts.Group).WithTags(Paths.WeatherForecasts.Tag);
        group.MapCreateWeatherForecast();
        group.MapDeleteWeatherForecast();
        group.MapGetAllWeatherForecasts();
        group.MapGetWeatherForecast();
        group.MapUpdateWeatherForecast();
        return app;
    }
}

public static class Paths
{
    private const string ApiBase = "";

    public static class WeatherForecasts
    {
        private const string Base = $"{ApiBase}";

        public const string Group = nameof(WeatherForecasts);
        public const string Tag = nameof(WeatherForecasts);

        public const string Create = Base;
        public const string GetAll = Base;
        public const string GetById = $"{Base}/{{id}}";
        public const string Update = $"{Base}/{{id}}";
        public const string Delete = $"{Base}/{{id}}";
    }
}
