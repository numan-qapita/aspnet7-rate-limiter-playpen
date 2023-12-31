using System.Net;
using System.Threading.RateLimiting;
using WebAPI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = (int)HttpStatusCode.TooManyRequests;

    options.AddPolicy("FixedWindow", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter($"{httpContext.Request.Path}_{httpContext.GetClientIp().ipAddress}", partition =>
            new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 1,
                Window = TimeSpan.FromSeconds(60)
            }));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .RequireRateLimiting("FixedWindow")
    .WithName("GetWeatherForecast")
    .WithOpenApi();

app.MapGet("/diagnostics/client-ip", (HttpContext httpContext) =>
    {
        var (ipAddress, source) = httpContext.GetClientIp();
        return Results.Ok(new { ipAddress, source, resolved = httpContext.ResolveClientIpAddress() });
    })
    .RequireRateLimiting("FixedWindow")
    .WithName("GetClientIpAddress")
    .WithOpenApi();

app.UseRateLimiter();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}