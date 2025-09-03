using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("Redis")
                   .WithRedisCommander();

var postgres = builder.AddPostgres("Postgres")
                      .WithPgAdmin();

var pgDb = postgres.AddDatabase("Chota");

var migrations = builder.AddProject<Chota_MigrationService>("Migrations")
    .WithReference(pgDb)
    .WithReference(postgres)
    .WaitFor(pgDb)
    .WaitFor(postgres);


builder.AddProject<Chota_Api>("Api")
       .WithUrlForEndpoint("https", url =>
       {
           url.DisplayText = "Scalar UI";
           url.Url = "/scalar";
       })
       .WithReference(redis)
       .WithReference(postgres)
       .WithReference(pgDb)
       .WithReference(migrations)
       .WaitFor(migrations);

builder.AddProject<Projects.Chota_RedisHydrator>("Chota-redishydrator");

builder.Build().Run();
