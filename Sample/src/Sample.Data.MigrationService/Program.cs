var builder = Host.CreateApplicationBuilder(args);

builder.AddAspireServiceDefaults();

// Override the folder where seed files here:  builder.AddMigrationWorker<SampleDbContext>("MySeedFolderLocation");
builder.AddMigrationWorker<SampleDbContext>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(MigrationWorker<SampleDbContext>.ActivitySourceName));

var dbOptions = builder.Configuration.GetSection(nameof(DatabaseOptions))
    .Get<DatabaseOptions>()
    ?? throw new InvalidOperationException("DatabaseOptions configuration section is missing.");

builder.AddSqlServerDbContext<SampleDbContext>(dbOptions.DatabaseName);

var host = builder.Build();

host.Run();
