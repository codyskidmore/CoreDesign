using Sample.Aspire.ServiceDefaults;
using CoreDesign.Identity.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Sample.Api.Infrastructure;

public static class Configuration
{
    public static WebApplicationBuilder AddConfiguration(this WebApplicationBuilder builder)
    {
        builder.AddDatabaseConfiguration();
        builder.AddAppSettings();

        builder.AddIdentityAuthentication();
        builder.AddAuthorizationPolicyConfiguration();
        builder.Services.AddApplicationInsightsTelemetry();

        builder.AddWeatherForecastsModule();

        builder.Services.AddOpenApi(options =>
            options.AddDocumentTransformer<BearerSecurityTransformer>());

        var dbOptions = builder.Configuration.GetSection(nameof(DatabaseOptions))
            .Get<DatabaseOptions>()
            ?? throw new InvalidOperationException("DatabaseOptions configuration is missing.");

        builder.AddAspireServiceDefaults();
        builder.AddSqlServerDbContext<SampleDbContext>(dbOptions.DatabaseName);

        builder.AddCaching();
        builder.AddCors();

        builder.Host.UseApplicationSerilog();

        return builder;
    }

    public static WebApplicationBuilder AddIdentityAuthentication(this WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
            builder.Services.AddIdentityClient(builder.Configuration);
        else
            builder.AddAzureEntraAuthentication();

        return builder;
    }

    public static WebApplicationBuilder AddAzureEntraAuthentication(this WebApplicationBuilder builder)
    {
        var instance = (builder.Configuration["AzureAd:Instance"] ?? "https://login.microsoftonline.com/").TrimEnd('/');
        var tenantId = builder.Configuration["AzureAd:TenantId"]!;
        var audience = builder.Configuration["AzureAd:Audience"]!;

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"{instance}/{tenantId}/v2.0";
                options.Audience = audience;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    RoleClaimType = "roles",
                    NameClaimType = "email"
                };
            });

        return builder;
    }

    public static WebApplicationBuilder AddCors(this WebApplicationBuilder builder)
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        builder.Services.AddCors(options =>
            options.AddDefaultPolicy(policy =>
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()));

        return builder;
    }

    public static IHostApplicationBuilder AddCaching(this IHostApplicationBuilder builder)
    {
        foreach (CacheConfig config in Enum.GetValues(typeof(CacheConfig)))
        {
            var cacheConfig = builder.Configuration
                .GetSection(Enum.GetName(typeof(CacheConfig), config) ?? string.Empty)
                .Get<CacheSettings>();
            builder.Services.AddApiCache(cacheConfig!);
        }

        return builder;
    }
}
