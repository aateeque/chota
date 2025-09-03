using IdGen.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using Chota.Api.Data;
using Chota.Api.Models;
using Chota.Api.Services;
using Chota.ServiceDefaults;

var chotaServiceBaseUrl = new Uri("https://cho.ta/");

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

builder.Services.AddHealthChecks();

// Register caching services
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

builder.AddNpgsqlDbContext<UrlDbContext>(connectionName: "Chota");

// Register services
builder.Services.AddTransient<IUrlValidator, UrlValidator>();
builder.Services.AddTransient<IUrlEncoder, Base62Encoder>();
builder.Services.AddSingleton<InMemoryUrlRepository>();
builder.Services.AddTransient<ICacheRepository, RedisUrlRepository>();
builder.Services.AddTransient<IPostgresUrlRepository, PostgresUrlRepository>();

builder.Services.AddIdGen(1);
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<IIdGeneratorService, IdGeneratorService>();
builder.Services.AddTransient<IUrlRepository, CompositeUrlRepository>();
builder.Services.AddTransient<IUrlService, UrlService>();

var app = builder.Build();

app.UseExceptionHandler();

app.MapOpenApi();

app.MapScalarApiReference();

app.MapHealthChecks("health");

app.MapPost("/api/create", async (CreateUrlRequest request, IUrlService urlService) =>
{
    var result = await urlService.Shorten(request.LongUrl);

    if (result.IsFailure)
    {
        return result.Error!.Code switch
        {
            "Error.Validation" => Results.BadRequest(new { Error = result.Error.Description }),
            _ => Results.Problem(title: "An error occurred", detail: result.Error.Description)
        };
    }

    return Results.Ok(new
    {
        result.Value,
        FullShortUrl = Uri.TryCreate(chotaServiceBaseUrl, result.Value!.ShortCode, out var uri)
    });
});

app.MapGet("/{shortCode}", async (string shortCode, IUrlService urlService, [FromServices] IHttpContextAccessor httpContextAccessor, [FromServices] IPostgresUrlRepository postgresUrlRepository) =>
{
    var result = await urlService.GetByShortCode(shortCode);

    if (result.IsFailure)
    {
        return result.Error!.Code switch
        {
            "Error.NotFound" => Results.NotFound(),
            "Error.Validation" => Results.BadRequest(new { Error = result.Error.Description }),
            _ => Results.Problem(title: "An error occurred", detail: result.Error.Description)
        };
    }

    if (httpContextAccessor.HttpContext!.Request.Headers.UserAgent.Contains("Mozilla"))
    {
        result.Value!.BrowserClickCount++;
    }
    else
    {
        // This is an API request - could add ApiClickCount tracking here
        result.Value!.ApiClickCount++;
    }

    // fire & forget analytics update
    _ = Task.Run(async () => await postgresUrlRepository.Update(result.Value));

    return Results.Redirect(result.Value!.LongUrl, permanent: true);
});

app.Run();
