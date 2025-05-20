using FlightManagementSystem.Core.Interfaces;
using FlightManagementSystem.Web.Hubs;
using FlightManagementSystem.Web.Services;

namespace FlightManagementSystem.Web.Extensions
{
    public static class SignalRExtensions
    {
        public static IServiceCollection AddSignalRServices(this IServiceCollection services)
        {
            // Add SignalR
            services.AddSignalR();

            // Register the hub service
            services.AddScoped<IFlightHubService, FlightHubService>();

            return services;
        }

        public static IApplicationBuilder UseSignalREndpoints(this IApplicationBuilder app)
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<FlightHub>("/flighthub");
            });

            return app;
        }
    }
}