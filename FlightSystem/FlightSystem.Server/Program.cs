using Microsoft.EntityFrameworkCore;
using FlightSystem.Server.Data;
using FlightSystem.Server.Hubs;
using FlightSystem.Server.Services;
using FlightSystem.Server.Middleware;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("🚀 Starting Flight Management System Server");

// Configuration Management - Environment specific settings
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Get connection string from configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

Console.WriteLine($"🌐 Server will run on: {builder.Configuration["ServerUrl"] ?? "http://localhost:5000"}");

// Force HTTP only on port 5000
builder.WebHost.UseUrls("http://localhost:5000");

// Add services to the container with proper lifetimes
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Entity Framework with proper configuration
builder.Services.AddDbContext<FlightDbContext>(options =>
{
    options.UseSqlite(connectionString);
    options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
    options.EnableDetailedErrors(builder.Environment.IsDevelopment());
}, ServiceLifetime.Scoped);

// Dependency Injection - Proper service registration
builder.Services.AddScoped<IFlightRepository, FlightRepository>();
builder.Services.AddScoped<IPassengerRepository, PassengerRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<ISeatRepository, SeatRepository>();

builder.Services.AddScoped<IFlightService, FlightService>();
builder.Services.AddScoped<ICheckInService, CheckInService>();
builder.Services.AddScoped<IConcurrencyService, ConcurrencyService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Add structured logging
builder.Services.AddScoped<IStructuredLogger, StructuredLogger>();

// SignalR with proper configuration
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
});

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:3000", "http://localhost:5000" };

        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Blazor Server
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// HTTP Client for external services
builder.Services.AddHttpClient();

// Logging configuration
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();
    if (builder.Environment.IsProduction())
    {
        // Only add EventLog in production on Windows
        try
        {
            logging.AddEventLog();
        }
        catch
        {
            // Ignore if EventLog is not available
        }
    }
});

// WebSocket Service for desktop clients
builder.Services.AddSingleton<IWebSocketService, WebSocketService>();

var app = builder.Build();

Console.WriteLine("🔧 Configuring application pipeline");

// Global Exception Handling
app.UseMiddleware<GlobalExceptionMiddleware>();

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

app.UseCors("AllowedOrigins");
app.UseStaticFiles();
app.UseRouting();

// Enable WebSockets
app.UseWebSockets();

// Map endpoints
app.MapControllers();
app.MapHub<FlightHub>("/flighthub");
app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// WebSocket endpoint for desktop clients
app.Map("/ws", async (HttpContext context, IWebSocketService webSocketService) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        await webSocketService.HandleWebSocketAsync(context);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.MapGet("/", () => Results.Redirect("/flights"));
app.MapGet("/admin", () => Results.Redirect("/dashboard"));

Console.WriteLine("💾 Initializing database...");

// Database initialization with EF only
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<FlightDbContext>();

        // Ensure database is created (this will create tables based on your models)
        await context.Database.EnsureCreatedAsync();
        Console.WriteLine("✅ Database created/verified successfully");

        // Initialize with seed data
        await DatabaseInitializer.InitializeAsync(context);
        Console.WriteLine("✅ Database initialization completed");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "❌ Database initialization failed");
        throw;
    }
}

// Start WebSocket service
var webSocketService = app.Services.GetRequiredService<IWebSocketService>();
await webSocketService.StartAsync();

Console.WriteLine("✅ Server configured successfully!");
Console.WriteLine("🌐 Server URLs:");
Console.WriteLine($"   - API: http://localhost:5000");
Console.WriteLine($"   - Swagger: http://localhost:5000/api-docs");
Console.WriteLine($"   - SignalR Hub: http://localhost:5000/flighthub");
Console.WriteLine($"   - WebSocket: http://localhost:5000/ws");
Console.WriteLine($"   - Web Display: http://localhost:5000/flights");
Console.WriteLine();

app.Run();