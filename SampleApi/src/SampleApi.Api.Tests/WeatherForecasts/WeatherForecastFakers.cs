using Bogus;
using SampleApi.Api.WeatherForecasts.Models;

namespace SampleApi.Api.Tests.WeatherForecasts;

public static class WeatherForecastFakers
{
    private static readonly string[] Summaries =
        ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

    public static Faker<WeatherForecast> WeatherForecast() =>
        new Faker<WeatherForecast>()
            .RuleFor(x => x.Id, _ => Ulid.NewUlid())
            .RuleFor(x => x.CreatedAt, f => f.Date.Past())
            .RuleFor(x => x.UpdatedAt, f => f.Date.Recent())
            .RuleFor(x => x.CreatedBy, _ => Guid.NewGuid())
            .RuleFor(x => x.UpdatedBy, _ => Guid.NewGuid())
            .RuleFor(x => x.IsDeleted, _ => false)
            .RuleFor(x => x.Location, f => f.Address.City())
            .RuleFor(x => x.Date, f => DateOnly.FromDateTime(f.Date.Future()))
            .RuleFor(x => x.TemperatureC, f => f.Random.Int(-20, 45))
            .RuleFor(x => x.Summary, f => f.PickRandom(Summaries));

    public static WeatherForecastRequest WeatherForecastRequest()
    {
        var f = new Faker();
        return new WeatherForecastRequest(
            f.Address.City(),
            DateOnly.FromDateTime(f.Date.Future()),
            f.Random.Int(-20, 45),
            f.PickRandom(Summaries)
        );
    }
}
