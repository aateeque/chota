using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis")
                   .WithRedisCommander();

var postgres = builder.AddPostgres("postgres")
                      .WithPgAdmin();

var pgDb = postgres.AddDatabase("Chota");

var migrations = builder.AddProject<Chota_MigrationService>("migrations")
    .WithReference(pgDb)
    .WithReference(postgres)
    .WaitFor(pgDb)
    .WaitFor(postgres);


builder.AddProject<Chota_Api>("api")
       .WithExternalHttpEndpoints()
       .WithReference(redis)
       .WithReference(postgres)
       .WithReference(pgDb)
       .WithReference(migrations)
       .WaitFor(migrations);

builder.Build().Run();
