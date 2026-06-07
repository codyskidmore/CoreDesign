using CoreDesign.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Projects;

namespace Sample.Aspire.AppHost;

internal static class AppHostExtensions
{
    internal static IResourceBuilder<SqlServerDatabaseResource> AddSqlDatabase(
        this IDistributedApplicationBuilder builder)
    {
        var dbOptions = builder.Configuration
            .GetSection(nameof(DatabaseOptions))
            .Get<DatabaseOptions>()!;

        var connResource = builder.AddConnectionString(dbOptions.ConnectionStringName);
        var sqlPassword = builder.AddParameter("SqlPassword", true);

        var dbServer = builder.AddSqlServer(dbOptions.HostName, password: sqlPassword)
            // Explicitly publish SQL Server on the configured host port.
            .WithEndpoint(name: "tcp", port: dbOptions.HostPort, targetPort: 1433, isProxied: false)
            .WithDataVolume("sampledb-data")
            .WithImageTag("latest")
            .WithLifetime(ContainerLifetime.Persistent)
            .WithImagePullPolicy(ImagePullPolicy.Always);

        return dbServer.AddDatabase(dbOptions.DatabaseName)
            .WithConnectionStringRedirection(connResource.Resource);
    }

    internal static IResourceBuilder<ProjectResource> AddIdentityWeb(
        this IDistributedApplicationBuilder builder)
    {
        return builder.AddProject<Sample_Identity_Web>("SampleIdentityWeb")
            // Pin to port 5003 so the issuer in appsettings.Development.json
            // ("https://localhost:5003") always matches the Aspire service URL
            // injected into the Blazor app via service discovery.
            .WithHttpsEndpoint(port: 5003, name: "https", isProxied: false)
            .WithUrlForEndpoint("https", u => u.DisplayText = "Sample Identity Web (Dev)");
    }

    internal static IResourceBuilder<ProjectResource> AddIdentityApi(
        this IDistributedApplicationBuilder builder)
    {
        return builder.AddProject<Sample_Identity_Api>("SampleIdentityApi")
            // Pin to port 5001 for a stable Scalar URL across Aspire restarts.
            // Issuer remains https://localhost:5003 (Identity Web) per appsettings.
            .WithHttpsEndpoint(port: 5001, name: "https", isProxied: false)
            .WithUrlForEndpoint("https", u => u.DisplayText = "Sample Identity API (Scalar)");
    }

    internal static IResourceBuilder<ProjectResource> AddSampleApi(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<SqlServerDatabaseResource> database)
    {
        return builder.AddProject<Sample_Api>("SampleApi")
            .WithUrlForEndpoint("https", u => u.DisplayText = "Sample API")
            .WithReference(database);
    }

    internal static IDistributedApplicationBuilder AddMigrationService(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<SqlServerDatabaseResource> database)
    {
        builder.AddProject<Sample_Data_MigrationService>("SampleMigrations")
            .WithReference(database);

        return builder;
    }

    internal static IDistributedApplicationBuilder AddSampleReact(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> identityWeb,
        IResourceBuilder<ProjectResource> sampleApi)
    {
        builder.AddViteApp("SampleReact", "../Sample.React")
            .WithNpm()
            .WithHttpEndpoint(port: 5173, env: "PORT", isProxied: false)
            .WithUrlForEndpoint("http", u => u.DisplayText = "Sample React UI")
            .WithReference(identityWeb)
            .WithReference(sampleApi)
            .WaitFor(identityWeb)
            .WaitFor(sampleApi);

        return builder;
    }

    internal static IDistributedApplicationBuilder AddSampleBlazor(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> identityWeb,
        IResourceBuilder<ProjectResource> sampleApi)
    {
        builder.AddProject<Sample_Blazor>("SampleBlazor")
            // Pin to port 7070 so redirect URIs registered in Identity.Web's
            // clients.json ("https://localhost:7070/...") always match.
            .WithHttpsEndpoint(port: 7070, name: "https", isProxied: false)
            .WithUrlForEndpoint("https", u => u.DisplayText = "Sample Blazor UI")
            .WithReference(identityWeb)
            .WithReference(sampleApi)
            .WaitFor(identityWeb)
            .WaitFor(sampleApi);

        return builder;
    }
}