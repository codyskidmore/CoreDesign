using CoreDesign.Shared.Infrastructure;
using SampleApi.Aspire.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("docker-compose");
builder.AddAppSettings();

var database = builder.AddSqlDatabase();

builder.AddIdentityApi();
builder.AddSampleApi(database);
builder.AddMigrationService(database);

builder.Build().Run();
