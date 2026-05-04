namespace SampleApi.Api.Infrastructure;

public enum CacheConfig
{
    WeatherForecastCache
}

public class CacheSettings
{
    public required string PolicyName { get; init; }
    public required int Expiration { get; init; }
    public required string TagName { get; init; }
    public required string[] QueryKeys { get; init; }
}

public static class CacheConfiguration
{
    public static IServiceCollection AddApiCache(this IServiceCollection services, CacheSettings cacheSettings)
    {
        services.AddOutputCache(x =>
        {
            x.AddBasePolicy(c => c.Cache());
            x.AddPolicy(cacheSettings.PolicyName, c =>
                c.Cache()
                    .Expire(TimeSpan.FromMinutes(cacheSettings.Expiration))
                    .SetVaryByQuery(cacheSettings.QueryKeys)
                    .Tag(cacheSettings.TagName));
        });
        return services;
    }
}
