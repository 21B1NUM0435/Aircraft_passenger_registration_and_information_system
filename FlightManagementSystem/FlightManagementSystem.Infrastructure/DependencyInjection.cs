using FlightManagementSystem.Core.Interfaces;
using FlightManagementSystem.Core.Services;
using FlightManagementSystem.Infrastructure.Data;
using FlightManagementSystem.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlightManagementSystem.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Add SQLite database
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(configuration.GetConnectionString("DefaultConnection") ??
                                "Data Source=flightmanagement.db"));

            // Register repositories
            services.AddScoped<IAircraftRepository, AircraftRepository>();
            services.AddScoped<IFlightRepository, FlightRepository>();
            services.AddScoped<IPassengerRepository, PassengerRepository>();
            services.AddScoped<IBookingRepository, BookingRepository>();
            services.AddScoped<ISeatRepository, SeatRepository>();
            services.AddScoped<ISeatAssignmentRepository, SeatAssignmentRepository>();
            services.AddScoped<IBoardingPassRepository, BoardingPassRepository>();
            services.AddScoped<ICheckInRecordRepository, CheckInRecordRepository>();

            // Register unit of work
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Register business services
            services.AddScoped<ICheckInService, CheckInService>();
            services.AddScoped<IFlightService, FlightService>();
            services.AddScoped<IBoardingPassService, BoardingPassService>();

            return services;
        }
    }
}