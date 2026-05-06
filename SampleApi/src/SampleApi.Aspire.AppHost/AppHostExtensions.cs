using CoreDesign.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Projects;

namespace SampleApi.Aspire.AppHost;

internal static class AppHostExtensions
{
    internal static IResourceBuilder<SqlServerDatabaseResource> AddSqlDatabase(
        this IDistributedApplicationBuilder builder)
    {
        var dbOptions = builder.Configuration
            .GetSection(nameof(DatabaseOptions))
            .Get<DatabaseOptions>();

        var sqlPassword = builder.AddParameter("SqlPassword", true);

        var dbServer = builder.AddSqlServer(dbOptions.HostName, password: sqlPassword, port: dbOptions.HostPort)
            .WithDataVolume("sampleapidb-data")
            .WithImageTag("latest")
            .WithLifetime(ContainerLifetime.Persistent)
            .WithImagePullPolicy(ImagePullPolicy.Always);

        return dbServer.AddDatabase(dbOptions.DatabaseName);
    }

    internal static IDistributedApplicationBuilder AddIdentityApi(
        this IDistributedApplicationBuilder builder)
    {
        builder.AddProject<SampleApi_Identity_Api>("SampleApiIdentityApi")
            .WithUrlForEndpoint("https", u => u.DisplayText = "SampleApi Identity (Dev)");

        return builder;
    }

    internal static IDistributedApplicationBuilder AddSampleApi(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<SqlServerDatabaseResource> database)
    {
        builder.AddProject<SampleApi_Api>("SampleApiApi")
            .WithUrlForEndpoint("https", u => u.DisplayText = "SampleApi API")
            .WithReference(database);

        return builder;
    }

    internal static IDistributedApplicationBuilder AddMigrationService(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<SqlServerDatabaseResource> database)
    {
        builder.AddProject<SampleApi_Data_MigrationService>("SampleApiMigrations")
            .WithReference(database);

        return builder;
    }
}