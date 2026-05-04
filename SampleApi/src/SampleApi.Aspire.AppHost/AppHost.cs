using CoreDesign.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("docker-compose");

builder.AddAppSettings();

var dbOptions = builder.Configuration.GetSection(nameof(DatabaseOptions))
    .Get<DatabaseOptions>();

var sqlPassword = builder.AddParameter("SqlPassword", true);

var dbServer = builder.AddSqlServer(dbOptions.HostName, password: sqlPassword, port: dbOptions.HostPort)
    .WithDataVolume("sampleapidb-data")
    .WithImageTag("latest")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithImagePullPolicy(ImagePullPolicy.Always);

var database = dbServer.AddDatabase(dbOptions.DatabaseName);

builder.AddProject<SampleApi_Identity_Api>("SampleApiIdentityApi")
    .WithUrlForEndpoint("https", u => u.DisplayText = "SampleApi Identity (Dev)");

builder.AddProject<SampleApi_Api>("SampleApiApi")
    .WithUrlForEndpoint("https", u => u.DisplayText = "SampleApi API")
    .WithReference(database);

builder.AddProject<SampleApi_Data_MigrationService>("SampleApiMigrations")
    .WithReference(database);

builder.Build().Run();
