using CloudUsage.Api.Data;
using CloudUsage.Api.Application.UsageEvents;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IUsageEventIngestionService, UsageEventIngestionService>();

var connectionString = builder.Configuration.GetConnectionString("UsageAnalyticsDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'UsageAnalyticsDatabase' is required. " +
        "Configure it with .NET User Secrets for local development.");

builder.Services.AddDbContext<UsageAnalyticsDbContext>(options => options.UseSqlServer(connectionString));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseExceptionHandler();
app.MapControllers();

app.Run();
