using FlightManagementSystem.Core.Interfaces;
using FlightManagementSystem.Infrastructure;
using FlightManagementSystem.Infrastructure.Data;
using FlightManagementSystem.Infrastructure.SocketServer;
using FlightManagementSystem.Web;
using FlightManagementSystem.Web.Extensions;
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

// Add Infrastructure services
builder.Services.AddInfrastructure(builder.Configuration);

// Add controllers and API Explorer
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        builder => builder
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

// Add SignalR services
builder.Services.AddSignalRServices();

// Add hosted service to start Socket Server
builder.Services.AddHostedService<SocketServerHostedService>();

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
app.UseRouting();
app.UseCors("AllowAllOrigins");

// Add anti-forgery middleware (REQUIRED for Blazor Server)
app.UseAntiforgery();

app.UseAuthorization();

// Map controllers and SignalR endpoints
app.MapControllers();
app.UseSignalREndpoints();
app.MapRazorComponents<FlightManagementSystem.Web.Components.App>()
    .AddInteractiveServerRenderMode();

// Initialize the database
await DatabaseInitializer.InitializeDatabaseAsync(app.Services);

app.Run();

// SocketServerHostedService.cs
namespace FlightManagementSystem.Web
{
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
            await _socketServer.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Socket Server");
            await _socketServer.StopAsync();
        }
    }
}