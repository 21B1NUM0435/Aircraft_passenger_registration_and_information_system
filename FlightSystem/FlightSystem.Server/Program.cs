using Microsoft.EntityFrameworkCore;
using FlightSystem.Server.Data;
using FlightSystem.Server.Hubs;


var builder = WebApplication.CreateBuilder(args);

// Add Concurrency Service for race condition handling
builder.Services.AddScoped<FlightSystem.Server.Services.IConcurrencyService, FlightSystem.Server.Services.ConcurrencyService>();


// Force HTTP only on port 5555 to avoid port conflicts
builder.WebHost.UseUrls("http://localhost:5000");

Console.WriteLine("🚀 Starting Flight Management System Server");
Console.WriteLine("🌐 Server will run on: http://localhost:5000");

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Entity Framework with SQLite
builder.Services.AddDbContext<FlightDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
});

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:5001", "http://localhost:5000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add Blazor Server (for web display)
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Add Flight Service
builder.Services.AddScoped<FlightSystem.Server.Services.IFlightService, FlightSystem.Server.Services.FlightService>();

builder.Services.AddHttpClient();

var app = builder.Build();

Console.WriteLine("🔧 Configuring application pipeline");

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Flight System API V1");
        c.RoutePrefix = "api-docs";
    });
    Console.WriteLine("📚 Swagger UI available at: /api-docs");
}

app.UseCors("AllowAll");
app.UseStaticFiles();
app.UseRouting();

// Map endpoints
app.MapControllers();
app.MapHub<FlightHub>("/flighthub");
app.MapRazorPages();
app.MapBlazorHub();
app.MapGet("/", () => Results.Redirect("/flights"));
app.MapGet("/admin", () => Results.Redirect("/dashboard"));

Console.WriteLine("💾 Initializing database...");

// Initialize database manually (instead of using EF migrations)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=flights.db";
await ManualDbInitializer.InitializeAsync(connectionString);

// Also initialize with EF context for any additional operations
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<FlightDbContext>();
    // Just ensure the context can connect
    try
    {
        await context.Database.CanConnectAsync();
        Console.WriteLine("✅ EF Context connection verified");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ EF Context warning: {ex.Message}");
    }
}

Console.WriteLine("✅ Server configured successfully!");
Console.WriteLine("🌐 Server URLs:");
Console.WriteLine($"   - API: http://localhost:5000");
Console.WriteLine($"   - Swagger: http://localhost:5000/api-docs");
Console.WriteLine($"   - SignalR Hub: http://localhost:5000/flighthub");
Console.WriteLine($"   - Web Display: http://localhost:5000/flights");
Console.WriteLine();

app.Run();