using FlightManagementSystem.Core.Interfaces;
using FlightManagementSystem.Infrastructure;
using FlightManagementSystem.Infrastructure.Data;
using FlightManagementSystem.Infrastructure.SocketServer;
using FlightManagementSystem.Web.Extensions;
using FlightManagementSystem.Web.Hubs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
// Configure SQLite connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                        "Data Source=flightmanagement.db";
builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// Add Infrastructure services (this includes repositories and business logic services)
builder.Services.AddInfrastructure(builder.Configuration);

// Add controllers and API Explorer
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// Add the Socket Server
builder.Services.AddSingleton<ISocketServer>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<FlightSocketServer>>();
    var port = builder.Configuration.GetValue<int>("SocketServer:Port", 5000);
    return new FlightSocketServer(logger, port);
});

// Add SignalR services (this will register FlightHubService)
builder.Services.AddSignalRServices();

// Add hosted service to start Socket Server
builder.Services.AddHostedService<SocketServerHostedService>();

// Add logging
builder.Services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.AddDebug();
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
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

// Map SignalR endpoints
app.MapHub<FlightHub>("/flighthub");

// Map Blazor components
app.MapRazorComponents<FlightManagementSystem.Web.Components.App>()
    .AddInteractiveServerRenderMode();

// Initialize the database
try
{
    await DatabaseInitializer.InitializeDatabaseAsync(app.Services);
    app.Logger.LogInformation("Database initialized successfully");
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "An error occurred while initializing the database");
}

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Debug);
});
app.Logger.LogInformation("=== APPLICATION STARTUP ===");
app.Logger.LogInformation("Socket Server Port: {Port}", builder.Configuration.GetValue<int>("SocketServer:Port", 5000));

// Test SignalR service registration
var hubService = app.Services.GetService<IFlightHubService>();
if (hubService != null)
{
    app.Logger.LogInformation("FlightHubService registered successfully");
}
else
{
    app.Logger.LogError("FlightHubService NOT registered - SignalR will not work!");
}

// Test Socket Server registration
var socketServer = app.Services.GetService<ISocketServer>();
if (socketServer != null)
{
    app.Logger.LogInformation("Socket Server registered successfully");
}
else
{
    app.Logger.LogError("Socket Server NOT registered!");
}

app.Logger.LogInformation("Application starting...");
app.Run();

// SocketServerHostedService - Define in the same file to avoid namespace issues
public class SocketServerHostedService : IHostedService
{
    private readonly ISocketServer _socketServer;
    private readonly ILogger<SocketServerHostedService> _logger;

    public SocketServerHostedService(ISocketServer socketServer, ILogger<SocketServerHostedService> logger)
    {
        _socketServer = socketServer;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Socket Server");
        try
        {
            await _socketServer.StartAsync(cancellationToken);
            _logger.LogInformation("Socket Server started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Socket Server");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Socket Server");
        try
        {
            await _socketServer.StopAsync();
            _logger.LogInformation("Socket Server stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop Socket Server");
        }
    }
}