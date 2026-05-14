# NuGet Package Extraction Recommendations

Analysis of the Sample solution for code that is generic enough to warrant extraction into the CoreDesign package suite.

---

## Recommendation 1: Migration worker base class (CoreDesign.Data)

**Action:** Add to the existing `CoreDesign.Data` package.

**Source:** `src/Sample.Data.MigrationService/MigrationService.cs`

### What to extract

`EnsureDatabaseAsync` and `RunMigrationAsync` are completely generic. They rely only on EF Core abstractions (`IRelationalDatabaseCreator`, `CreateExecutionStrategy`, `MigrateAsync`) and the OpenTelemetry activity API. The only thing tying them to this project is the concrete `SampleDbContext` type parameter, which disappears once the class is made generic.

The proposed shape is an abstract base class:

```csharp
public abstract class MigrationWorker<TContext>(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationWorker<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    public const string ActivitySourceName = "Migrations";
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("Migrating database", ActivityKind.Client);
        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

            await EnsureDatabaseAsync(dbContext, ct);
            await RunMigrationAsync(dbContext, ct);
            await SeedAsync(dbContext, ct);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }

        lifetime.StopApplication();
    }

    protected virtual Task SeedAsync(TContext dbContext, CancellationToken ct)
        => Task.CompletedTask;

    private static async Task EnsureDatabaseAsync(TContext dbContext, CancellationToken ct)
    {
        var dbCreator = dbContext.GetService<IRelationalDatabaseCreator>();
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            if (!await dbCreator.ExistsAsync(ct))
                await dbCreator.CreateAsync(ct);
        });
    }

    private static async Task RunMigrationAsync(TContext dbContext, CancellationToken ct)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(() => dbContext.Database.MigrateAsync(ct));
    }
}
```

A consuming project subclasses it and optionally overrides `SeedAsync`:

```csharp
public class AppMigrationWorker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime lifetime,
    ILogger<AppMigrationWorker> logger)
    : MigrationWorker<AppDbContext>(serviceProvider, lifetime, logger)
{
    protected override async Task SeedAsync(AppDbContext dbContext, CancellationToken ct)
    {
        // application-specific seeding
    }
}
```

Registration in `Program.cs`:

```csharp
builder.Services.AddHostedService<AppMigrationWorker>();
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(MigrationWorker<AppDbContext>.ActivitySourceName));
```

### What NOT to extract

The current `SeedDatabaseAsync` method in the Sample project uses reflection to resolve entity types from JSON filenames (`"Sample.Api.WeatherForecasts.Models.WeatherForecast, Sample.Api"`). This naming convention is brittle and entirely application-specific. The virtual `SeedAsync` override above is the right boundary: the library handles ensure-and-migrate, the consuming application handles seeding.

### Why it belongs in CoreDesign.Data

Every project using `CoreDesign.Data` with EF Core will need a migration runner. The execution strategy wrapping (`CreateExecutionStrategy`) and OpenTelemetry tracing are subtle but important details that developers tend to miss. Putting them in the base class means they are correct by default in every project.

---

## Recommendation 2: BearerTokenHandler (CoreDesign.Identity.Client)

**Action:** Add to the existing `CoreDesign.Identity.Client` package.

**Source:** `src/Sample.Blazor/Services/BearerTokenHandler.cs`

### What to extract

The handler is 29 lines with zero application-specific dependencies. The entire class can be moved as-is:

```csharp
public sealed class BearerTokenHandler(IHttpContextAccessor httpContextAccessor)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext is not null)
        {
            var accessToken = await httpContext.GetTokenAsync("access_token");

            if (!string.IsNullOrEmpty(accessToken))
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

A registration extension should accompany it so consumers do not need to remember the two-step setup:

```csharp
public static IHttpClientBuilder AddBearerTokenHandler(this IHttpClientBuilder builder)
{
    builder.Services.AddTransient<BearerTokenHandler>();
    return builder.AddHttpMessageHandler<BearerTokenHandler>();
}
```

Usage in a Blazor Server app becomes:

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<MyApiClient>(c => c.BaseAddress = new Uri("https://my-api"))
    .AddBearerTokenHandler();
```

### Why it belongs in CoreDesign.Identity.Client

`CoreDesign.Identity.Client` already owns the client-side identity story for API servers (JWT validation, token injection middleware). `BearerTokenHandler` is the Blazor Server equivalent of that same concern. Placing it in the same package keeps all token-forwarding logic in one place and makes it immediately discoverable for developers building Blazor frontends against a CoreDesign-backed API.

---

## Things reviewed and not recommended for extraction

### ServiceDefaults (Sample.Aspire.ServiceDefaults)

The code is entirely generic but packaging it would work against how Aspire is designed. Microsoft's guidance is deliberately to copy this project into each solution as an `<IsAspireSharedProject>` so teams can customize health check paths, tracing filters, and resilience settings freely. If it became a NuGet dependency, every OpenTelemetry or Aspire version bump would require a package release and every consumer would lose the ability to modify the defaults without forking. Leave it as a copy-and-own solution project.

### Serilog setup (Sample.Api/Infrastructure/Serilog.cs)

Nine lines wrapping `UseSerilog` with a hard-coded ApplicationInsights sink. The ApplicationInsights dependency is opinionated and not every project will want it. Better treated as a documented recipe than a packaged abstraction.

### Scalar setup (Sample.Api/Infrastructure/Scalar.cs)

Two method calls wrapping `MapOpenApi` and `MapScalarApiReference` with a theme preference. Too thin to justify a package. Copy-paste in 30 seconds.

### Output cache helper (Sample.Api/Infrastructure/Cache.cs)

A `CacheSettings` record and one `AddOutputCache` wrapper. The settings record is useful but there is not enough logic here to reduce meaningful duplication. Copy-paste.

### Environment-keyed authorization policies (Sample.Api/Infrastructure/AuthorizationPolicyConfiguration.cs)

The pattern of mapping different role names per environment (Development, UAT, Production) is a genuinely good idea, but the current implementation is entirely hard-coded to this application's role constants (`DevAdmin`, `UATAdmin`, `AdminUsers`, etc.). Making it generic would require the consumer to supply all role mappings through configuration or a builder API, at which point the package is little more than boilerplate around the built-in `AddAuthorization`. Document the pattern; do not package it.

---

## Summary

| Component | Recommendation | Target package | Effort |
| --- | --- | --- | --- |
| `MigrationWorker<TContext>` base class | Extract | `CoreDesign.Data` | Medium |
| `BearerTokenHandler` | Extract | `CoreDesign.Identity.Client` | Low |
| `ServiceDefaults` | Keep as solution project | N/A | None |
| Serilog setup | Document as recipe | N/A | None |
| Scalar setup | Document as recipe | N/A | None |
| Output cache helper | Document as recipe | N/A | None |
| Authorization policy configuration | Document as pattern | N/A | None |
