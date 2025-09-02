using Chota.Api.Data;
using Chota.MigrationService;
using Chota.ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<Worker>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

builder.AddNpgsqlDbContext<UrlDbContext>(connectionName: "Chota");

var host = builder.Build();
host.Run();