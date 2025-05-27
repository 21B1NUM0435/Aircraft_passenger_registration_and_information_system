using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightManagementSystem.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlightManagementSystem.Infrastructure.Data
{
    public static class DatabaseInitializer
    {
        public static async Task InitializeDatabaseAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var services = scope.ServiceProvider;
            try
            {
                var context = services.GetRequiredService<ApplicationDbContext>();

                // Ensure database is created and run migrations
                await context.Database.MigrateAsync();

                // Seed data if none exists
                await SeedDataAsync(context);
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();
                logger.LogError(ex, "An error occurred while initializing the database.");
                throw;
            }
        }

        private static async Task SeedDataAsync(ApplicationDbContext context)
        {
            await context.Database.EnsureCreatedAsync();

            // Add sample data only if tables are empty
            if (!await context.Aircraft.AnyAsync())
            {
                // Add sample aircraft
                var aircraft1 = new Aircraft
                {
                    AircraftId = "AC001",
                    Model = "Boeing 737-800",
                    Capacity = 189,
                    ManufacturedDate = new DateTime(2015, 5, 15)
                };

                var aircraft2 = new Aircraft
                {
                    AircraftId = "AC002",
                    Model = "Airbus A320",
                    Capacity = 150,
                    ManufacturedDate = new DateTime(2018, 3, 10)
                };

                await context.Aircraft.AddRangeAsync(aircraft1, aircraft2);
                await context.SaveChangesAsync();

                // Add sample flights
                var flight1 = new Flight
                {
                    FlightNumber = "MR101",
                    AircraftId = "AC001",
                    DepartureTime = DateTime.Now.AddHours(5),
                    ArrivalTime = DateTime.Now.AddHours(7),
                    Origin = "ULN",
                    Destination = "PEK",
                    Status = FlightStatus.CheckingIn
                };

                var flight2 = new Flight
                {
                    FlightNumber = "MR102",
                    AircraftId = "AC002",
                    DepartureTime = DateTime.Now.AddHours(3),
                    ArrivalTime = DateTime.Now.AddHours(6),
                    Origin = "ULN",
                    Destination = "ICN",
                    Status = FlightStatus.CheckingIn
                };

                await context.Flights.AddRangeAsync(flight1, flight2);
                await context.SaveChangesAsync();

                // Add seats for aircraft 1
                var seatsAC001 = new List<Seat>();
                // Economy seats
                for (int row = 10; row <= 30; row++)
                {
                    foreach (var col in new[] { "A", "B", "C", "D", "E", "F" })
                    {
                        seatsAC001.Add(new Seat
                        {
                            SeatId = $"S{row}{col}-AC001",
                            AircraftId = "AC001",
                            SeatNumber = $"{row}{col}",
                            SeatClass = SeatClass.Economy,
                            Price = 100.00m
                        });
                    }
                }

                // Business seats
                for (int row = 1; row <= 3; row++)
                {
                    foreach (var col in new[] { "A", "B", "C", "D" })
                    {
                        seatsAC001.Add(new Seat
                        {
                            SeatId = $"S{row}{col}-AC001",
                            AircraftId = "AC001",
                            SeatNumber = $"{row}{col}",
                            SeatClass = SeatClass.Business,
                            Price = 300.00m
                        });
                    }
                }

                // Add seats for aircraft 2
                var seatsAC002 = new List<Seat>();
                // Economy seats
                for (int row = 10; row <= 25; row++)
                {
                    foreach (var col in new[] { "A", "B", "C", "D", "E", "F" })
                    {
                        seatsAC002.Add(new Seat
                        {
                            SeatId = $"S{row}{col}-AC002",
                            AircraftId = "AC002",
                            SeatNumber = $"{row}{col}",
                            SeatClass = SeatClass.Economy,
                            Price = 100.00m
                        });
                    }
                }

                // Business seats
                for (int row = 1; row <= 2; row++)
                {
                    foreach (var col in new[] { "A", "C", "D", "F" })
                    {
                        seatsAC002.Add(new Seat
                        {
                            SeatId = $"S{row}{col}-AC002",
                            AircraftId = "AC002",
                            SeatNumber = $"{row}{col}",
                            SeatClass = SeatClass.Business,
                            Price = 300.00m
                        });
                    }
                }

                await context.Seats.AddRangeAsync(seatsAC001);
                await context.Seats.AddRangeAsync(seatsAC002);
                await context.SaveChangesAsync();

                // Add staff
                var staff1 = new AirlineStaff
                {
                    StaffId = "ST001",
                    Name = "John Smith",
                    Username = "jsmith",
                    PasswordHash = "hashed_password" // In real app, use proper password hashing
                };

                var staff2 = new AirlineStaff
                {
                    StaffId = "ST002",
                    Name = "Alice Johnson",
                    Username = "ajohnson",
                    PasswordHash = "hashed_password" // In real app, use proper password hashing
                };

                await context.AirlineStaff.AddRangeAsync(staff1, staff2);
                await context.SaveChangesAsync();

                // Add check-in counters
                var counter1 = new CheckInCounter
                {
                    CounterId = "C001",
                    StaffId = "ST001",
                    Terminal = "A",
                    Location = "Terminal A, Counter 1",
                    Status = CounterStatus.Open
                };

                var counter2 = new CheckInCounter
                {
                    CounterId = "C002",
                    StaffId = "ST002",
                    Terminal = "A",
                    Location = "Terminal A, Counter 2",
                    Status = CounterStatus.Open
                };

                await context.CheckInCounters.AddRangeAsync(counter1, counter2);
                await context.SaveChangesAsync();

                // Add sample passengers and bookings
                var passenger1 = new Passenger
                {
                    PassengerId = "P001",
                    FirstName = "Bat",
                    LastName = "Bold",
                    PassportNumber = "MN12345678",
                    PassportExpirationDate = DateTime.Now.AddYears(5),
                    DateOfBirth = new DateTime(1985, 3, 15),
                    Gender = "Male",
                    Email = "batbold@example.com",
                    Phone = "+976 99112233",
                    Address = "Ulaanbaatar, Mongolia"
                };

                var passenger2 = new Passenger
                {
                    PassengerId = "P002",
                    FirstName = "Saran",
                    LastName = "Tuya",
                    PassportNumber = "MN98765432",
                    PassportExpirationDate = DateTime.Now.AddYears(7),
                    DateOfBirth = new DateTime(1990, 7, 21),
                    Gender = "Female",
                    Email = "sarantuya@example.com",
                    Phone = "+976 99445566",
                    Address = "Ulaanbaatar, Mongolia"
                };

                await context.Passengers.AddRangeAsync(passenger1, passenger2);
                await context.SaveChangesAsync();

                // Add bookings
                var booking1 = new Booking
                {
                    BookingReference = "BK001",
                    PassengerId = "P001",
                    FlightNumber = "MR101",
                    BookingDate = DateTime.Now.AddDays(-5),
                    CheckInStatus = CheckInStatus.NotCheckedIn,
                    PaymentStatus = PaymentStatus.Completed,
                    TotalPrice = 350.00m
                };

                var booking2 = new Booking
                {
                    BookingReference = "BK002",
                    PassengerId = "P002",
                    FlightNumber = "MR101",
                    BookingDate = DateTime.Now.AddDays(-3),
                    CheckInStatus = CheckInStatus.NotCheckedIn,
                    PaymentStatus = PaymentStatus.Completed,
                    TotalPrice = 275.00m
                };

                await context.Bookings.AddRangeAsync(booking1, booking2);
                await context.SaveChangesAsync();
            }
        }
    }
}
