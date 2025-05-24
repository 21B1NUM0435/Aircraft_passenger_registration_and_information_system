using FlightManagementSystem.Core.Interfaces;
using FlightManagementSystem.Infrastructure;
using FlightManagementSystem.Infrastructure.Data;
using FlightManagementSystem.Infrastructure.WebSocketServer; // Updated namespace
using FlightManagementSystem.Web.Extensions;
using FlightManagementSystem.Web.Hubs;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ===== ENHANCED LOGGING CONFIGURATION =====
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole(options =>
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "HH:mm:ss.fff ";
    });
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Debug);

    // Add custom filters for better debugging
    logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
    logging.AddFilter("FlightManagementSystem", LogLevel.Debug);
    logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
});

Console.WriteLine("🚀 === FLIGHT MANAGEMENT SYSTEM STARTUP (WebSocket Edition) ===");
Console.WriteLine($"⏰ Startup Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine($"🏗️ Environment: {builder.Environment.EnvironmentName}");

// ===== ALL SERVICE REGISTRATIONS BEFORE builder.Build() =====

// Configure SQLite connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                        "Data Source=flightmanagement.db";
builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;
Console.WriteLine($"💾 Database: {connectionString}");

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// Add Infrastructure services (this includes repositories and business logic services)
builder.Services.AddInfrastructure(builder.Configuration);
Console.WriteLine("✅ Infrastructure services registered");

// Add controllers and API Explorer
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
Console.WriteLine("✅ Controllers and API services registered");

// Configure JSON serialization options for security (BEFORE builder.Build())
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

// Add Blazor components with anti-forgery
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add anti-forgery services
builder.Services.AddAntiforgery();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});
Console.WriteLine("✅ CORS policy configured");

// ===== REPLACE SOCKET SERVER WITH WEBSOCKET SERVER =====
var webSocketPort = builder.Configuration.GetValue<int>("WebSocketServer:Port", 8080);
Console.WriteLine($"🌐 WebSocket Server will use port: {webSocketPort}");

builder.Services.AddSingleton<ISocketServer>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<FlightWebSocketServer>>();
    return new FlightWebSocketServer(logger, webSocketPort);
});

// REMOVE SignalR services - we're using pure WebSocket now
// builder.Services.AddSignalRServices();
Console.WriteLine("✅ WebSocket server registered (SignalR removed)");

// Add hosted service to start WebSocket Server
builder.Services.AddHostedService<WebSocketServerHostedService>();

// ===== BUILD THE APPLICATION (After all service registrations) =====
var app = builder.Build();
Console.WriteLine("🏗️ Application built successfully");

// ===== POST-BUILD CONFIGURATION AND MIDDLEWARE =====

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Flight Management API V1");
        c.RoutePrefix = "api-docs";
    });
    Console.WriteLine("✅ Development tools configured (Swagger at /api-docs)");
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    Console.WriteLine("✅ Production error handling configured");
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Important: Middleware order matters!
app.UseRouting();
app.UseCors("AllowAllOrigins");
app.UseAuthentication();  // If using authentication
app.UseAuthorization();
app.UseAntiforgery();     // Add anti-forgery middleware after authorization

// Map controllers
app.MapControllers();
Console.WriteLine("✅ API controllers mapped");

// REMOVE SignalR Hub mapping - we're using pure WebSocket now
// app.MapHub<FlightHub>("/flighthub");
Console.WriteLine("✅ SignalR Hub removed - using WebSocket instead");

// Map Blazor components
app.MapRazorComponents<FlightManagementSystem.Web.Components.App>()
    .AddInteractiveServerRenderMode();
Console.WriteLine("✅ Blazor components mapped");

// ===== STARTUP DIAGNOSTICS AND VERIFICATION =====
Console.WriteLine("\n🔍 === SYSTEM DIAGNOSTICS ===");

// Test WebSocket Server registration
var webSocketServer = app.Services.GetService<ISocketServer>();
if (webSocketServer != null)
{
    Console.WriteLine("✅ WebSocket Server registered successfully");

    // Test if we can get stats (if implemented)
    try
    {
        if (webSocketServer is FlightWebSocketServer flightWebSocketServer)
        {
            var stats = flightWebSocketServer.GetStats();
            Console.WriteLine($"📊 WebSocket Server stats: {stats.ConnectedClients} clients, {stats.TotalMessagesSent} messages sent");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ WebSocket Server stats error: {ex.Message}");
    }
}
else
{
    Console.WriteLine("❌ WebSocket Server NOT registered!");
}

// SignalR is removed - no longer testing
Console.WriteLine("ℹ️ SignalR removed - using pure WebSocket communication");

// Test Database Connection
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    var canConnect = await dbContext.Database.CanConnectAsync();
    if (canConnect)
    {
        Console.WriteLine("✅ Database connection verified");

        var flightCount = await dbContext.Flights.CountAsync();
        var passengerCount = await dbContext.Passengers.CountAsync();
        Console.WriteLine($"📊 Database contains: {flightCount} flights, {passengerCount} passengers");
    }
    else
    {
        Console.WriteLine("❌ Cannot connect to database!");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Database connection error: {ex.Message}");
}

// Initialize the database with enhanced error handling
try
{
    Console.WriteLine("💾 Initializing database...");
    await DatabaseInitializer.InitializeDatabaseAsync(app.Services);
    Console.WriteLine("✅ Database initialized successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Database initialization error: {ex.Message}");
    Console.WriteLine($"   Stack trace: {ex.StackTrace}");
}

// ===== FINAL STARTUP MESSAGES =====
Console.WriteLine("\n🚀 === APPLICATION READY ===");
Console.WriteLine($"🌐 Web Interface: https://localhost:7275");
Console.WriteLine($"📋 API Documentation: https://localhost:7275/api-docs");
Console.WriteLine($"🔌 WebSocket Server: ws://localhost:{webSocketPort}");
Console.WriteLine($"🧪 WebSocket Test Page: http://localhost:{webSocketPort}");
Console.WriteLine("🎯 Ready for WebSocket connections from Windows applications and web browsers");
Console.WriteLine("⏰ Press Ctrl+C to shutdown\n");

// Add graceful shutdown handling
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("\n🛑 === APPLICATION SHUTDOWN ===");
    Console.WriteLine("⏰ Shutdown initiated...");

    try
    {
        var socket = app.Services.GetService<ISocketServer>();
        if (socket != null)
        {
            socket.StopAsync().Wait(TimeSpan.FromSeconds(5));
            Console.WriteLine("✅ WebSocket server stopped");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Error stopping WebSocket server: {ex.Message}");
    }

    Console.WriteLine("✅ Shutdown complete");
});

app.Run();

// WebSocketServerHostedService - Updated for WebSocket
public class WebSocketServerHostedService : IHostedService
{
    private readonly ISocketServer _webSocketServer;
    private readonly ILogger<WebSocketServerHostedService> _logger;

    public WebSocketServerHostedService(ISocketServer webSocketServer, ILogger<WebSocketServerHostedService> logger)
    {
        _webSocketServer = webSocketServer;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🌐 Starting WebSocket Server Host Service");
        Console.WriteLine("🌐 WebSocket Server Host Service starting...");

        try
        {
            await _webSocketServer.StartAsync(cancellationToken);
            _logger.LogInformation("✅ WebSocket Server started successfully");
            Console.WriteLine("✅ WebSocket Server Host Service started successfully");

            // Log WebSocket server details
            if (_webSocketServer is FlightWebSocketServer flightWebSocketServer)
            {
                var stats = flightWebSocketServer.GetStats();
                Console.WriteLine($"📊 WebSocket Server Stats: {stats.ConnectedClients} clients, {stats.TotalMessagesSent} messages sent");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to start WebSocket Server");
            Console.WriteLine($"❌ WebSocket Server Host Service failed to start: {ex.Message}");

            // Don't throw - allow the application to continue running
            // The WebSocket server failure shouldn't bring down the entire application
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🌐 Stopping WebSocket Server Host Service");
        Console.WriteLine("🌐 WebSocket Server Host Service stopping...");

        try
        {
            await _webSocketServer.StopAsync();
            _logger.LogInformation("✅ WebSocket Server stopped successfully");
            Console.WriteLine("✅ WebSocket Server Host Service stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to stop WebSocket Server");
            Console.WriteLine($"❌ WebSocket Server Host Service failed to stop: {ex.Message}");
        }
    }
}