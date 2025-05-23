using FlightManagementSystem.Core.Interfaces;
using FlightManagementSystem.Web.Services;

namespace FlightManagementSystem.Web.Extensions
{
    public static class SignalRExtensions
    {
        public static IServiceCollection AddSignalRServices(this IServiceCollection services)
        {
            // Add SignalR
            services.AddSignalR();

            // Register the hub service - make it optional since it might not be needed for simple flight display
            services.AddScoped<IFlightHubService, FlightHubService>();

            return services;
        }
    }
}