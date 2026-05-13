using CoreDesign.Shared.Infrastructure;
using SampleApi.Aspire.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("docker-compose");
builder.AddAppSettings();

var database = builder.AddSqlDatabase();

var identityWeb = builder.AddIdentityWeb();
builder.AddIdentityApi();
var sampleApi   = builder.AddSampleApi(database);

builder.AddMigrationService(database);
builder.AddSampleBlazor(identityWeb, sampleApi);

builder.Build().Run();
