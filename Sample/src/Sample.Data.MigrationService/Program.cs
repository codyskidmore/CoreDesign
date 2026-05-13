using CoreDesign.Shared.Infrastructure;
using Sample.Api.Data;
using Sample.Data.MigrationService;
using Sample.Aspire.ServiceDefaults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.AddAspireServiceDefaults();

builder.AddConfiguration();

builder.Services.AddHostedService<MigrationService>();
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(MigrationService.ActivitySourceName));

var dbOptions = builder.Configuration.GetSection(nameof(DatabaseOptions))
    .Get<DatabaseOptions>();

builder.AddSqlServerDbContext<SampleDbContext>(dbOptions.DatabaseName);

var host = builder.Build();

host.Run();
