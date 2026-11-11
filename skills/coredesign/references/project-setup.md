# Building a project with every CoreDesign feature

This guide builds a complete solution from nothing: an Aspire AppHost, an API with all CoreDesign packages wired in, a migration service that migrates and seeds the database, and a local OIDC identity server. Follow the steps in order; each one compiles on its own. Replace `MyApp`, `Widget` and the port numbers with the project's own names. The CoreDesign Sample (`Sample/src/` in the CoreDesign repository) is a working copy of exactly this layout, using `WeatherForecast` as its feature.

## Contents

1. Solution layout
2. Prerequisites and SDK pin
3. Shared configuration
4. ServiceDefaults
5. API project: packages and global usings
6. Data layer: schema, DbContext, current user, seed factory
7. API startup: Program, Configuration, App
8. Identity server project
9. Migration service project
10. AppHost
11. First feature (vertical slice)
12. Seed data
13. First run and verification
14. Feature checklist

## 1. Solution layout

```
MyApp/
├── global.json
├── MyApp.slnx
└── src/
    ├── Shared/                       appsettings.json, appsettings.Development.json, identities.json, clients.json
    ├── MyApp.AppHost/                Aspire orchestration
    ├── MyApp.ServiceDefaults/        Aspire service defaults (OpenTelemetry, health checks, resilience)
    ├── MyApp.Api/                    the API: entities, handlers, endpoints, DbContext, migrations
    ├── MyApp.Api.Tests/              xUnit tests for handlers
    ├── MyApp.Data.MigrationService/  migrates and seeds, then exits
    └── MyApp.Identity.Web/           CoreDesign.Identity.Server for development sign-in
```

The DbContext, entities and EF migrations live in the API project. The migration service references the API project to reuse them.

## 2. Prerequisites and SDK pin

- .NET 11 SDK, Docker (for the PostgreSQL container), and the EF Core tool: `dotnet tool install -g dotnet-ef`.
- Pin the SDK so every machine builds with .NET 11:

```json
{
  "sdk": {
    "version": "11.0.100",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

Every project targets `net11.0` with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`. Do not set `<LangVersion>`.

## 3. Shared configuration

All projects link one set of settings files from `src/Shared/` so values never drift. In each project's `.csproj`:

```xml
<ItemGroup>
    <Content Include="..\Shared\appsettings.json">
        <Link>appsettings.json</Link>
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
    <Content Include="..\Shared\appsettings.Development.json">
        <Link>appsettings.Development.json</Link>
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
</ItemGroup>
```

`src/Shared/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
  },
  "AllowedHosts": "*",
  "DatabaseOptions": {
    "HostName": "myapp-postgres",
    "DatabaseName": "myapp-db",
    "HostPort": 55432,
    "ConnectionStringName": "myappdb"
  },
  "IdentityApi": {
    "BaseUrl": "https://MyAppIdentityWeb"
  },
  "CoreDesign": {
    "Identity": {
      "Issuer": "https://localhost:5003",
      "Audience": "https://api.myapp.local"
    },
    "IdentityWebHost": {
      "Issuer": "https://localhost:5003",
      "Audience": "https://api.myapp.local",
      "TokenLifetimeHours": 8,
      "IdentitiesFilePath": "identities.json",
      "ClientsFilePath": "clients.json"
    }
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<tenant-id>",
    "ClientId": "<client-id>",
    "Audience": "api://<client-id>"
  },
  "Cors": {
    "AllowedOrigins": [ "http://localhost:5173" ]
  },
  "Serilog": {
    "Destructure": [
      { "Name": "ToMaximumDepth", "Args": { "maximumDestructuringDepth": 5 } },
      { "Name": "ToMaximumStringLength", "Args": { "maximumStringLength": 500 } },
      { "Name": "ToMaximumCollectionCount", "Args": { "maximumCollectionCount": 10 } }
    ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": { "Microsoft": "Warning", "Microsoft.Hosting.Lifetime": "Information" }
    },
    "WriteTo": [ { "Name": "Console" } ],
    "Enrich": [ "FromLogContext" ]
  }
}
```

`src/Shared/appsettings.Development.json` holds the development identity credentials that the API's token injection middleware uses:

```json
{
  "IdentityApi": {
    "BaseUrl": "https://localhost:5003",
    "ClientId": "myapp-api-dev",
    "Username": "admin@myapp.local",
    "Password": "Password1!"
  }
}
```

The Serilog `Destructure` limits matter: the logging decorators log whole return values, and without a depth cap an EF entity with navigation properties can produce enormous log entries.

## 4. ServiceDefaults

Create it from the Aspire template (`dotnet new aspire-servicedefaults -n MyApp.ServiceDefaults`). It provides OpenTelemetry, health checks and service discovery. Installed Aspire templates can lag behind: after generating this project and the AppHost (`dotnet new aspire-apphost`), set `<TargetFramework>net11.0</TargetFramework>`, move `Microsoft.Extensions.Http.Resilience` and `Microsoft.Extensions.ServiceDiscovery` to 11.0.0, and set the AppHost SDK to `Aspire.AppHost.Sdk/13.6.5` or later. The API and the migration service reference it and call its extension method (`AddServiceDefaults()` in the template; the CoreDesign Sample renames it `AddAspireServiceDefaults()`).

## 5. API project: packages and global usings

`MyApp.Api.csproj` (SDK `Microsoft.NET.Sdk.Web`):

```xml
<ItemGroup>
    <PackageReference Include="CoreDesign.Shared" Version="11.0.0" />
    <PackageReference Include="CoreDesign.Data" Version="11.0.0" />
    <PackageReference Include="CoreDesign.Logging" Version="11.0.0" />
    <PackageReference Include="CoreDesign.ExceptionHandling" Version="11.0.0" />
    <PackageReference Include="CoreDesign.Identity.Client" Version="11.0.0" />

    <PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" Version="13.6.5" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="11.0.0" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="11.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.15" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.15">
        <PrivateAssets>all</PrivateAssets>
        <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Scalar.AspNetCore" Version="2.16.15" />
    <PackageReference Include="Serilog.AspNetCore" Version="10.0.0" />
</ItemGroup>

<ItemGroup>
    <ProjectReference Include="..\MyApp.ServiceDefaults\MyApp.ServiceDefaults.csproj" />
</ItemGroup>
```

Use SQL Server instead by swapping the Npgsql packages for `Aspire.Microsoft.EntityFrameworkCore.SqlServer` and `UseNpgsql` for `UseSqlServer`; nothing in CoreDesign depends on the provider. Optional: add `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` to inspect the generated logging decorators under `obj/`.

`GlobalUsings.cs`:

```csharp
global using CoreDesign.Data.Infrastructure;
global using CoreDesign.Data.Interfaces;
global using CoreDesign.Data.Repositories;
global using CoreDesign.ExceptionHandling;
global using CoreDesign.Logging;
global using CoreDesign.Shared.Infrastructure;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.OutputCaching;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore.Metadata.Builders;
global using MyApp.Api.Data;
global using MyApp.Api.Infrastructure;
```

## 6. Data layer

`Data/Schemas.cs` (an enum keeps the schema name out of magic strings):

```csharp
namespace MyApp.Api.Data;

public enum Schemas { MyApp }
```

`Data/MyAppDbContext.cs`, inheriting `CoreDesignDbContext` (not `DbContext`), which wires the audit interceptor and normalizes every `DateTime` to UTC:

```csharp
namespace MyApp.Api.Data;

public class MyAppDbContext(DbContextOptions<MyAppDbContext> options) : CoreDesignDbContext(options)
{
    public DbSet<Widget> Widgets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(nameof(Schemas.MyApp));
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MyAppDbContext).Assembly);
    }
}
```

`Infrastructure/CurrentUserAccessor.cs`, which supplies the user ID the audit interceptor writes into `CreatedBy` and `UpdatedBy`:

```csharp
namespace MyApp.Api.Infrastructure;

public class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public Guid UserId
    {
        get
        {
            var oid = httpContextAccessor.HttpContext?.User.FindFirst("oid")?.Value;
            return Guid.TryParse(oid, out var id) ? id : Guid.Empty;
        }
    }
}
```

`Data/MyAppSeedFactory.cs`, which lets the `dotnet seed` tool create the DbContext (it needs a public parameterless constructor and is found by reflection):

```csharp
using CoreDesign.Data;

namespace MyApp.Api.Data;

public class MyAppSeedFactory : CoreDesignSeedFactory<MyAppDbContext>
{
    protected override void Configure(DbContextOptionsBuilder<MyAppDbContext> builder, string connectionString)
        => builder.UseNpgsql(connectionString);

    protected override MyAppDbContext Create(DbContextOptions<MyAppDbContext> options) => new(options);
}
```

`Data/MyAppDbContextFactory.cs`, which lets `dotnet ef` create the DbContext without starting the app. The API's startup throws when the Aspire connection string is missing, and it is missing whenever the EF tools run outside Aspire. Creating, removing and checking migrations never connects to the database, so a placeholder connection string is enough; a real one from user secrets is used when present:

```csharp
using Microsoft.EntityFrameworkCore.Design;

namespace MyApp.Api.Data;

public class MyAppDbContextFactory : IDesignTimeDbContextFactory<MyAppDbContext>
{
    private const string DesignTimeConnectionString = "Host=localhost;Database=design-time";

    public MyAppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<MyAppDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var databaseName = configuration.GetSection(nameof(DatabaseOptions)).Get<DatabaseOptions>()?.DatabaseName;
        var connectionString = (databaseName is null ? null : configuration.GetConnectionString(databaseName))
            ?? DesignTimeConnectionString;

        var options = new DbContextOptionsBuilder<MyAppDbContext>().UseNpgsql(connectionString).Options;
        return new MyAppDbContext(options);
    }
}
```

`Infrastructure/Permissions.cs`. Permission strings are passed to `RequireAuthorization` and must match the Entra app role values exactly in production:

```csharp
namespace MyApp.Api.Infrastructure;

public static class Permissions
{
    public const string WidgetsRead  = "widgets:read";
    public const string WidgetsWrite = "widgets:write";
}
```

## 7. API startup

`Program.cs`:

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.AddConfiguration();

    var app = builder.Build();
    app.AddContextConfiguration();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

`Infrastructure/Configuration.cs` registers every CoreDesign service. The order that matters: `AddCoreDesignData` before the DbContext (the DbContext needs the `AuditInterceptor`), and the feature modules (which end with `DecorateWithLogging()`) after everything their handlers depend on is registered.

```csharp
using CoreDesign.Identity.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace MyApp.Api.Infrastructure;

public static class Configuration
{
    public static WebApplicationBuilder AddConfiguration(this WebApplicationBuilder builder)
    {
        // CoreDesign.Shared: appsettings and DatabaseOptions
        builder.AddAppSettings();
        builder.AddDatabaseConfiguration();
        builder.AddServiceDefaults();

        // CoreDesign.Data: current user + audit interceptor
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddCoreDesignData<CurrentUserAccessor>();

        var dbOptions = builder.Configuration.GetSection(nameof(DatabaseOptions)).Get<DatabaseOptions>()
            ?? throw new InvalidOperationException("DatabaseOptions configuration is missing.");
        var connectionString = builder.Configuration.GetConnectionString(dbOptions.DatabaseName)
            ?? throw new InvalidOperationException($"Connection string '{dbOptions.DatabaseName}' is missing.");

        builder.Services.AddDbContextPool<MyAppDbContext>((sp, options) =>
            options.UseNpgsql(connectionString)
                .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

        // CoreDesign.ExceptionHandling: global ProblemDetails
        builder.Services.AddCoreDesignExceptionHandling();
        builder.Services.AddGeneratedProblemMappings();

        // CoreDesign.Identity.Client: dev identity server locally, Entra elsewhere
        builder.AddIdentityAuthentication();

        // OpenAPI with the Bearer scheme on authenticated operations
        builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecurityTransformer>());

        builder.Services.AddOutputCache();
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
            policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
                .AllowAnyHeader()
                .AllowAnyMethod()));

        builder.Host.UseSerilog((context, services, config) => config
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());

        // Feature modules last: each registers repositories and handlers.
        builder.AddWidgetsModule();

        // CoreDesign.Logging: exactly once, after every [LoggingDecorator] interface is registered.
        builder.Services.DecorateWithLogging();

        return builder;
    }

    private static WebApplicationBuilder AddIdentityAuthentication(this WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddIdentityClient(builder.Configuration);
            return builder;
        }

        var instance = (builder.Configuration["AzureAd:Instance"] ?? "https://login.microsoftonline.com/").TrimEnd('/');
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"{instance}/{builder.Configuration["AzureAd:TenantId"]}/v2.0";
                options.Audience = builder.Configuration["AzureAd:Audience"];
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    NameClaimType = "email"
                };
            });
        builder.Services.AddScoped<IClaimsTransformation, RolesToPermissionsTransformation>();
        builder.Services.AddPermissionAuthorization();
        return builder;
    }
}
```

`Infrastructure/RolesToPermissionsTransformation.cs` turns Entra `roles` claims into the `permissions` claims the authorization handler checks (only used outside Development):

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace MyApp.Api.Infrastructure;

public class RolesToPermissionsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = (ClaimsIdentity)principal.Identity!;
        foreach (var role in principal.FindAll("roles").ToList())
            identity.AddClaim(new Claim("permissions", role.Value));
        return Task.FromResult(principal);
    }
}
```

If the project calls `DecorateWithLogging()` inside a module instead (the Sample does, because it has a single module), keep it to one call in the whole application.

`Infrastructure/App.cs`. `UseExceptionHandler()` goes first so it wraps everything; token injection must come before authentication:

```csharp
using CoreDesign.Identity.Client;
using Scalar.AspNetCore;

namespace MyApp.Api.Infrastructure;

public static class App
{
    public static WebApplication AddContextConfiguration(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        app.UseCors();
        app.UseLocalBearerTokenInjection();   // Development only; no effect elsewhere
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseOutputCache();

        app.MapWidgetsModule();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference().AllowAnonymous();
        }

        return app;
    }
}
```

## 8. Identity server project

`MyApp.Identity.Web` (SDK `Microsoft.NET.Sdk.Web`) references `CoreDesign.Identity.Server` 11.0.0 and links the shared settings plus `identities.json` and `clients.json` the same way as section 3.

`Program.cs`:

```csharp
using CoreDesign.Identity.Server;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddIdentityServerWebHost(builder.Configuration);

var app = builder.Build();
app.MapIdentityServerWebHost();
app.Run();
```

`src/Shared/identities.json` (development users; passwords are plaintext, so never reuse real ones):

```json
[
  {
    "userId": "11111111-1111-1111-1111-111111111111",
    "username": "admin@myapp.local",
    "password": "Password1!",
    "email": "admin@myapp.local",
    "name": "Admin User",
    "givenName": "Admin",
    "familyName": "User",
    "permissions": [ "widgets:read", "widgets:write" ],
    "customClaims": {}
  },
  {
    "userId": "22222222-2222-2222-2222-222222222222",
    "username": "reader@myapp.local",
    "password": "Password1!",
    "email": "reader@myapp.local",
    "name": "Read Only",
    "givenName": "Read",
    "familyName": "Only",
    "permissions": [ "widgets:read" ],
    "customClaims": {}
  }
]
```

`src/Shared/clients.json` (one password-grant client for the API's development token injection; add an `authorization_code` client with PKCE for each browser app, see `references/identity.md`):

```json
[
  {
    "clientId": "myapp-api-dev",
    "tokenEndpointAuthMethod": "none",
    "allowedGrantTypes": [ "password" ],
    "allowedRedirectUris": [],
    "allowedPostLogoutRedirectUris": [],
    "allowedScopes": [ "openid", "profile", "email", "https://api.myapp.local" ],
    "requirePkce": false
  }
]
```

## 9. Migration service project

`MyApp.Data.MigrationService` (SDK `Microsoft.NET.Sdk`, `<OutputType>Exe</OutputType>`) references the API project and ServiceDefaults, plus `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3, `Microsoft.EntityFrameworkCore.Relational` 10.0.15 and `Microsoft.Extensions.Hosting` 11.0.0. It links the shared settings and copies every seed file with a glob, so new seed files need no project edit:

```xml
<ItemGroup>
    <Content Include="SeedData\*.json">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
</ItemGroup>
```

`Program.cs`:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.AddAppSettings();
builder.AddServiceDefaults();

// No signed-in user here: audit fields get Guid.Empty.
builder.Services.AddCoreDesignData<SystemUserAccessor>();

// Ensures the database exists, applies migrations, seeds from SeedData/, then stops the host.
builder.AddMigrationWorker<MyAppDbContext>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(MigrationWorker<MyAppDbContext>.ActivitySourceName));

var dbOptions = builder.Configuration.GetSection(nameof(DatabaseOptions)).Get<DatabaseOptions>()
    ?? throw new InvalidOperationException("DatabaseOptions configuration section is missing.");
var connectionString = builder.Configuration.GetConnectionString(dbOptions.DatabaseName)
    ?? throw new InvalidOperationException($"Connection string '{dbOptions.DatabaseName}' is missing.");

builder.Services.AddDbContext<MyAppDbContext>((sp, options) =>
    options.UseNpgsql(connectionString)
        .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

builder.Build().Run();
```

`GlobalUsings.cs` for this project:

```csharp
global using CoreDesign.Data.Infrastructure;
global using CoreDesign.Shared.Infrastructure;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using MyApp.Api.Data;
```

## 10. AppHost

`MyApp.AppHost.csproj` uses `<Project Sdk="Aspire.AppHost.Sdk/13.6.5">`, has a `UserSecretsId`, references `Aspire.Hosting.PostgreSQL` 13.6.5 and `CoreDesign.Shared` 11.0.0, references the API, migration service and identity projects, and links the shared settings.

`AppHost.cs`:

```csharp
using CoreDesign.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);
builder.AddAppSettings();

var dbOptions = builder.Configuration.GetSection(nameof(DatabaseOptions)).Get<DatabaseOptions>()!;

// A static connection string in user secrets, redirected into the Aspire database resource,
// lets the app, the dotnet seed tool and database IDEs all use the same connection.
var connection = builder.AddConnectionString(dbOptions.ConnectionStringName);
var password = builder.AddParameter("PostgresPassword", secret: true);

var database = builder.AddPostgres(dbOptions.HostName, password: password)
    .WithEndpoint(name: "tcp", port: dbOptions.HostPort, targetPort: 5432, isProxied: false)
    .WithDataVolume("myapp-pg-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .AddDatabase(dbOptions.DatabaseName)
    .WithConnectionStringRedirection(connection.Resource);

// Fixed port so the issuer (https://localhost:5003) always matches.
var identity = builder.AddProject<MyApp_Identity_Web>("MyAppIdentityWeb")
    .WithHttpsEndpoint(port: 5003, name: "https", isProxied: false);

var migrations = builder.AddProject<MyApp_Data_MigrationService>("MyAppMigrations")
    .WithReference(database)
    .WaitFor(database);

builder.AddProject<MyApp_Api>("MyAppApi")
    .WithReference(database)
    .WithReference(identity)
    .WaitForCompletion(migrations);

builder.Build().Run();
```

Set the secrets once:

```
dotnet user-secrets set "Parameters:PostgresPassword" "<password>" --project src/MyApp.AppHost
dotnet user-secrets set "ConnectionStrings:myappdb" "Host=127.0.0.1;Port=55432;Database=myapp-db;Username=postgres;Password={PostgresPassword}" --project src/MyApp.AppHost
```

## 11. First feature (vertical slice)

One folder per operation. `Shared/` holds only the entity and its configuration; requests, responses and handlers belong to the operation that uses them. Every file below is complete, including its namespace. Add these two lines to `GlobalUsings.cs` so the DbContext, `Configuration.cs` and `App.cs` can see the feature:

```csharp
global using MyApp.Api.Widgets;
global using MyApp.Api.Widgets.Shared;
```

```
Widgets/
├── Shared/Widget.cs, WidgetConfiguration.cs
├── Create/CreateWidgetHandler.cs, Endpoint.cs, Request.cs
├── GetById/GetWidgetHandler.cs, Endpoint.cs
├── GetAll/GetAllWidgetsHandler.cs, Endpoint.cs
├── Update/UpdateWidgetHandler.cs, Endpoint.cs
├── Delete/DeleteWidgetHandler.cs, Endpoint.cs
└── WidgetsModule.cs
```

`Widgets/Shared/Widget.cs`:

```csharp
namespace MyApp.Api.Widgets.Shared;

public class Widget : BaseEntity
{
    public required string Name { get; set; }
    public decimal Price { get; set; }
}
```

`Widgets/Shared/WidgetConfiguration.cs`:

```csharp
namespace MyApp.Api.Widgets.Shared;

public class WidgetConfiguration : BaseEntityConfiguration<Widget>
{
    public override void Configure(EntityTypeBuilder<Widget> builder)
    {
        base.Configure(builder);   // key, soft-delete filter, audit columns
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Price).HasPrecision(10, 2);
        builder.HasIndex(e => e.Name).IsUnique();
        builder.ToTable("Widgets", nameof(Schemas.MyApp));
    }
}
```

`Widgets/WidgetsModule.cs` registers the slice and holds the route constants, so no route string is repeated:

```csharp
using MyApp.Api.Widgets.Create;
using MyApp.Api.Widgets.Delete;
using MyApp.Api.Widgets.GetAll;
using MyApp.Api.Widgets.GetById;
using MyApp.Api.Widgets.Update;

namespace MyApp.Api.Widgets;

public static class WidgetsModule
{
    public static IHostApplicationBuilder AddWidgetsModule(this IHostApplicationBuilder builder)
    {
        var s = builder.Services;
        s.AddTransient<IReadRepository<MyAppDbContext, Widget>, ReadRepository<MyAppDbContext, Widget>>();
        s.AddTransient<ICudRepository<MyAppDbContext, Widget>, CudRepository<MyAppDbContext, Widget>>();

        s.AddTransient<ICreateWidgetHandler, CreateWidgetHandler>();
        s.AddTransient<IGetWidgetHandler, GetWidgetHandler>();
        s.AddTransient<IGetAllWidgetsHandler, GetAllWidgetsHandler>();
        s.AddTransient<IUpdateWidgetHandler, UpdateWidgetHandler>();
        s.AddTransient<IDeleteWidgetHandler, DeleteWidgetHandler>();
        return builder;
    }

    public static IEndpointRouteBuilder MapWidgetsModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(WidgetPaths.Group).WithTags(WidgetPaths.Group);
        group.MapCreateWidget();
        group.MapGetWidget();
        group.MapGetAllWidgets();
        group.MapUpdateWidget();
        group.MapDeleteWidget();
        return app;
    }
}

public static class WidgetPaths
{
    public const string Group = "widgets";
    public const string CacheTag = "widgets";
    public const string Collection = "";
    public const string Item = "{id}";
}

public record WidgetResponse(Ulid Id, string Name, decimal Price, DateTime UpdatedAt)
{
    public static WidgetResponse From(Widget w) => new(w.Id, w.Name, w.Price, w.UpdatedAt);
}
```

`WidgetResponse` is shared here only because every operation returns the same shape. When an operation needs a different shape, give it its own `Response.cs` in its folder.

### Create

`Widgets/Create/Request.cs`:

```csharp
namespace MyApp.Api.Widgets.Create;

public record Request(string Name, decimal Price)
{
    public Widget ToNewEntity() => new() { Name = Name, Price = Price };
}
```

`Widgets/Create/CreateWidgetHandler.cs`:

```csharp
namespace MyApp.Api.Widgets.Create;

[LoggingDecorator]
public interface ICreateWidgetHandler
{
    Task<Union<Widget, BadRequestMessage, InvalidOperationMessage>> CreateAsync(Request request, CancellationToken ct);
}

public class CreateWidgetHandler(
    IReadRepository<MyAppDbContext, Widget> readRepository,
    ICudRepository<MyAppDbContext, Widget> cudRepository) : ICreateWidgetHandler
{
    public async Task<Union<Widget, BadRequestMessage, InvalidOperationMessage>> CreateAsync(
        Request request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return new BadRequestMessage("Name is required.");

        // The unique index is the real guarantee; this turns the common case into a clear 409.
        if (await readRepository.GetAsync(w => w.Name == request.Name, null, ct) is not null)
            return new InvalidOperationMessage($"A widget named '{request.Name}' already exists.");

        var entity = request.ToNewEntity();
        if (!await cudRepository.InsertAsync(entity, ct))
            return new BadRequestMessage("Failed to create widget.");

        return entity;
    }
}
```

`Widgets/Create/Endpoint.cs`. Cache eviction is a side effect of one case, so it runs before the switch:

```csharp
namespace MyApp.Api.Widgets.Create;

public static class Endpoint
{
    public static IEndpointRouteBuilder MapCreateWidget(this IEndpointRouteBuilder app)
    {
        app.MapPost(WidgetPaths.Collection, async (
                [FromBody] Request request,
                ICreateWidgetHandler handler,
                IOutputCacheStore cache,
                CancellationToken ct) =>
            {
                var result = await handler.CreateAsync(request, ct);
                if (result is Widget)
                    await cache.EvictByTagAsync(WidgetPaths.CacheTag, ct);

                return result switch
                {
                    Widget widget                     => Results.Created($"/{WidgetPaths.Group}/{widget.Id}", WidgetResponse.From(widget)),
                    BadRequestMessage badInput        => Results.BadRequest(badInput.Message),
                    InvalidOperationMessage duplicate => Results.Conflict(duplicate.Message),
                };
            })
            .Produces<WidgetResponse>(StatusCodes.Status201Created)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces<string>(StatusCodes.Status409Conflict)
            .RequireAuthorization(Permissions.WidgetsWrite);

        return app;
    }
}
```

### GetById

`Widgets/GetById/GetWidgetHandler.cs`:

```csharp
namespace MyApp.Api.Widgets.GetById;

[LoggingDecorator]
public interface IGetWidgetHandler
{
    Task<Union<Widget, NotFoundMessage>> GetAsync(Ulid id, CancellationToken ct);
}

public class GetWidgetHandler(IReadRepository<MyAppDbContext, Widget> repository) : IGetWidgetHandler
{
    public async Task<Union<Widget, NotFoundMessage>> GetAsync(Ulid id, CancellationToken ct)
    {
        var widget = await repository.GetAsync(w => w.Id == id, null, ct);
        if (widget is null)
            return new NotFoundMessage($"Widget {id} not found.");
        return widget;
    }
}
```

`Widgets/GetById/Endpoint.cs` (route parameters of type `Ulid` bind directly):

```csharp
namespace MyApp.Api.Widgets.GetById;

public static class Endpoint
{
    public static IEndpointRouteBuilder MapGetWidget(this IEndpointRouteBuilder app)
    {
        app.MapGet(WidgetPaths.Item, async (Ulid id, IGetWidgetHandler handler, CancellationToken ct) =>
            {
                var result = await handler.GetAsync(id, ct);
                return result switch
                {
                    Widget widget         => Results.Ok(WidgetResponse.From(widget)),
                    NotFoundMessage error => Results.NotFound(error.Message),
                };
            })
            .Produces<WidgetResponse>()
            .Produces<string>(StatusCodes.Status404NotFound)
            .CacheOutput(policy => policy.Tag(WidgetPaths.CacheTag))
            .RequireAuthorization(Permissions.WidgetsRead);

        return app;
    }
}
```

### GetAll

An empty list is a valid answer, not an error, so this handler returns the list directly instead of a union. `Widgets/GetAll/GetAllWidgetsHandler.cs`:

```csharp
namespace MyApp.Api.Widgets.GetAll;

[LoggingDecorator]
public interface IGetAllWidgetsHandler
{
    Task<IList<Widget>> GetAllAsync(CancellationToken ct);
}

public class GetAllWidgetsHandler(IReadRepository<MyAppDbContext, Widget> repository) : IGetAllWidgetsHandler
{
    public Task<IList<Widget>> GetAllAsync(CancellationToken ct) =>
        repository.GetAllAsync(orderBy: q => q.OrderBy(w => w.Name), token: ct);
}
```

`Widgets/GetAll/Endpoint.cs`:

```csharp
namespace MyApp.Api.Widgets.GetAll;

public static class Endpoint
{
    public static IEndpointRouteBuilder MapGetAllWidgets(this IEndpointRouteBuilder app)
    {
        app.MapGet(WidgetPaths.Collection, async (IGetAllWidgetsHandler handler, CancellationToken ct) =>
                Results.Ok((await handler.GetAllAsync(ct)).Select(WidgetResponse.From)))
            .Produces<IEnumerable<WidgetResponse>>()
            .CacheOutput(policy => policy.Tag(WidgetPaths.CacheTag))
            .RequireAuthorization(Permissions.WidgetsRead);

        return app;
    }
}
```

### Update

Load the entity without includes, apply the request, save. `Widgets/Update/UpdateWidgetHandler.cs`:

```csharp
namespace MyApp.Api.Widgets.Update;

public record Request(string Name, decimal Price)
{
    public void Apply(Widget widget)
    {
        widget.Name = Name;
        widget.Price = Price;
    }
}

[LoggingDecorator]
public interface IUpdateWidgetHandler
{
    Task<Union<Widget, NotFoundMessage, BadRequestMessage>> UpdateAsync(Ulid id, Request request, CancellationToken ct);
}

public class UpdateWidgetHandler(
    IReadRepository<MyAppDbContext, Widget> readRepository,
    ICudRepository<MyAppDbContext, Widget> cudRepository) : IUpdateWidgetHandler
{
    public async Task<Union<Widget, NotFoundMessage, BadRequestMessage>> UpdateAsync(
        Ulid id, Request request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return new BadRequestMessage("Name is required.");

        var widget = await readRepository.GetAsync(w => w.Id == id, null, ct);
        if (widget is null)
            return new NotFoundMessage($"Widget {id} not found.");

        request.Apply(widget);
        if (!await cudRepository.UpdateAsync(widget, ct))
            return new BadRequestMessage("Failed to update widget.");

        return widget;
    }
}
```

`Widgets/Update/Endpoint.cs`:

```csharp
namespace MyApp.Api.Widgets.Update;

public static class Endpoint
{
    public static IEndpointRouteBuilder MapUpdateWidget(this IEndpointRouteBuilder app)
    {
        app.MapPut(WidgetPaths.Item, async (
                Ulid id,
                [FromBody] Request request,
                IUpdateWidgetHandler handler,
                IOutputCacheStore cache,
                CancellationToken ct) =>
            {
                var result = await handler.UpdateAsync(id, request, ct);
                if (result is Widget)
                    await cache.EvictByTagAsync(WidgetPaths.CacheTag, ct);

                return result switch
                {
                    Widget widget              => Results.Ok(WidgetResponse.From(widget)),
                    NotFoundMessage notFound   => Results.NotFound(notFound.Message),
                    BadRequestMessage badInput => Results.BadRequest(badInput.Message),
                };
            })
            .Produces<WidgetResponse>()
            .Produces<string>(StatusCodes.Status404NotFound)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .RequireAuthorization(Permissions.WidgetsWrite);

        return app;
    }
}
```

### Delete

Soft delete, returning `Success` because there is nothing to return. `Widgets/Delete/DeleteWidgetHandler.cs`:

```csharp
namespace MyApp.Api.Widgets.Delete;

[LoggingDecorator]
public interface IDeleteWidgetHandler
{
    Task<Union<Success, NotFoundMessage>> DeleteAsync(Ulid id, CancellationToken ct);
}

public class DeleteWidgetHandler(ICudRepository<MyAppDbContext, Widget> repository) : IDeleteWidgetHandler
{
    public async Task<Union<Success, NotFoundMessage>> DeleteAsync(Ulid id, CancellationToken ct) =>
        await repository.SoftDeleteAsync(id, ct)
            ? new Success()
            : new NotFoundMessage($"Widget {id} not found.");
}
```

`Widgets/Delete/Endpoint.cs`:

```csharp
namespace MyApp.Api.Widgets.Delete;

public static class Endpoint
{
    public static IEndpointRouteBuilder MapDeleteWidget(this IEndpointRouteBuilder app)
    {
        app.MapDelete(WidgetPaths.Item, async (
                Ulid id,
                IDeleteWidgetHandler handler,
                IOutputCacheStore cache,
                CancellationToken ct) =>
            {
                var result = await handler.DeleteAsync(id, ct);
                if (result is Success)
                    await cache.EvictByTagAsync(WidgetPaths.CacheTag, ct);

                return result switch
                {
                    Success               => Results.NoContent(),
                    NotFoundMessage error => Results.NotFound(error.Message),
                };
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces<string>(StatusCodes.Status404NotFound)
            .RequireAuthorization(Permissions.WidgetsWrite);

        return app;
    }
}
```

### Migration

Create the migration with the tooling (never by hand):

```
dotnet ef migrations add AddWidgets --project src/MyApp.Api --startup-project src/MyApp.Api --output-dir Data/Migrations
```

If the build is locked because Aspire is running, stop the AppHost and run the command again.

## 12. Seed data

Create `src/MyApp.Data.MigrationService/SeedData/MyApp.Api.Widgets.Shared.Widget.json`. The file name is the entity's fully qualified type name; a mismatch is skipped with only a warning. Include fixed `Id` values (Ulids) so reseeding is idempotent:

```json
[
  {
    "Name": "Sprocket",
    "Price": 9.99,
    "Id": "01JZ0000000000000000000001",
    "CreatedAt": "2026-01-01T00:00:00Z",
    "UpdatedAt": "2026-01-01T00:00:00Z",
    "CreatedBy": "00000000-0000-0000-0000-000000000000",
    "UpdatedBy": "00000000-0000-0000-0000-000000000000",
    "IsDeleted": false
  }
]
```

Rows whose `Id` already exists are skipped, including soft-deleted ones. Later, use `dotnet seed export` to capture data edited through the app (see `references/seedtool.md`).

## 13. First run and verification

1. Run the AppHost. The migration service should finish with exit code 0 before the API starts.
2. Open Scalar on the API. In Development, local requests get a bearer token injected automatically for the `IdentityApi:Username` user.
3. Create a widget, then read it back. The API log shows the handler call at Information.
4. Request a missing id. The log shows `GetAsync returned {"Message":...,"$type":"NotFoundMessage"}` at Warning, and the response is 404.
5. Hit an endpoint that throws an unplanned exception. The response is an RFC 7807 500 with `detail` only in Development.
6. Run `dotnet test`.

## 14. Feature checklist

Use this to confirm a project uses every CoreDesign feature correctly.

| Feature | Where | Done when |
|---|---|---|
| Shared settings | `src/Shared`, linked into every project | One source of truth for `DatabaseOptions`, identity and Serilog |
| `AddAppSettings`, `AddDatabaseConfiguration` | API, migration service, AppHost | `DatabaseOptions` binds in every host |
| `BaseEntity`, `BaseEntityConfiguration<T>` | Every entity | `base.Configure(builder)` called first |
| `CoreDesignDbContext` | DbContext | Inherited, configurations applied from the assembly |
| `AddCoreDesignData<T>` + `AuditInterceptor` | API (`CurrentUserAccessor`), migration service (`SystemUserAccessor`) | Interceptor added to every DbContext registration |
| Repositories | Feature modules | Registered per entity, injected into handlers |
| `Union<...>` results | Every handler | Expected failures are result records, endpoints switch exhaustively |
| `[LoggingDecorator]` + `DecorateWithLogging()` | Handler interfaces, one call at startup | Handlers contain no log statements |
| `[Redact]` / `[Suppress]` | Sensitive parameters, noisy methods | Secrets never appear in logs |
| ExceptionHandling | `AddCoreDesignExceptionHandling`, `AddGeneratedProblemMappings`, `UseExceptionHandler` first | Unplanned exceptions return ProblemDetails |
| `[ProblemMapping]` | Custom and third-party exception types | Only for exceptions, never for expected outcomes |
| Identity.Server | `MyApp.Identity.Web` on a fixed port | Issuer matches `CoreDesign:Identity:Issuer` |
| Identity.Client | `AddIdentityClient` (dev), `AddPermissionAuthorization` (prod), `UseLocalBearerTokenInjection` | Endpoints use `RequireAuthorization(Permissions.X)` |
| `BearerSecurityTransformer` | `AddOpenApi` | Scalar shows the Bearer scheme |
| `MigrationWorker` | Migration service | Exits 0 after migrate and seed |
| Seed files | `SeedData/` with a glob include | Fully qualified type names, fixed Ids |
| `CoreDesignSeedFactory` + `dotnet seed` | API project, repo root `coredesign.seedtool.json` | `dotnet seed diff` runs cleanly |
| Tests | `MyApp.Api.Tests` | Handlers tested with mocked repositories (`references/testing.md`) |
