var builder = Host.CreateApplicationBuilder(args);

builder.AddAspireServiceDefaults();

builder.Services.AddHostedService<SampleMigrationWorker>();
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(MigrationWorker<SampleDbContext>.ActivitySourceName));

var dbOptions = builder.Configuration.GetSection(nameof(DatabaseOptions))
    .Get<DatabaseOptions>()
    ?? throw new InvalidOperationException("DatabaseOptions configuration section is missing.");

builder.AddSqlServerDbContext<SampleDbContext>(dbOptions.DatabaseName);

var host = builder.Build();

host.Run();
