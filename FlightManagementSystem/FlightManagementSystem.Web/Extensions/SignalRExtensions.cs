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

            // Register the hub service as SINGLETON for proper broadcasting
            services.AddSingleton<IFlightHubService, FlightHubService>();

            return services;
        }
    }
}