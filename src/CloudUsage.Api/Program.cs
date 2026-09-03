using CloudUsage.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

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
app.MapControllers();

app.Run();
