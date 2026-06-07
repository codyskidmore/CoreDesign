var builder = Host.CreateApplicationBuilder(args);

builder.AddAppSettings();
builder.AddAspireServiceDefaults();
builder.Services.AddCoreDesignData<SystemUserAccessor>();

// Override the folder where seed files here:  builder.AddMigrationWorker<SampleDbContext>("MySeedFolderLocation");
builder.AddMigrationWorker<SampleDbContext>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(MigrationWorker<SampleDbContext>.ActivitySourceName));

var dbOptions = builder.Configuration.GetSection(nameof(DatabaseOptions))
    .Get<DatabaseOptions>()
    ?? throw new InvalidOperationException("DatabaseOptions configuration section is missing.");

var connectionString = builder.Configuration.GetConnectionString(dbOptions.DatabaseName)
    ?? throw new InvalidOperationException($"Connection string '{dbOptions.DatabaseName}' is missing.");

builder.Services.AddDbContext<SampleDbContext>((serviceProvider, options) =>
    options.UseSqlServer(connectionString)
        .AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>()));

var host = builder.Build();

host.Run();
