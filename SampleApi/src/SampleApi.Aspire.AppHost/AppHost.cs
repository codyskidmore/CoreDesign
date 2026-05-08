using CoreDesign.Shared.Infrastructure;
using SampleApi.Aspire.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("docker-compose");
builder.AddAppSettings();

var database = builder.AddSqlDatabase();

var identityApi = builder.AddIdentityWeb();
var sampleApi   = builder.AddSampleApi(database);

builder.AddMigrationService(database);
builder.AddSampleBlazor(identityApi, sampleApi);

builder.Build().Run();
