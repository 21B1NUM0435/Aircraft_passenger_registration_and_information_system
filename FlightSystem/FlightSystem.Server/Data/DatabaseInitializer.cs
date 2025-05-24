using FlightSystem.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace FlightSystem.Server.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(FlightDbContext context)
    {
        try
        {
            Console.WriteLine("🔨 Applying database migrations...");

            // Apply any pending migrations (this will create tables)
            await context.Database.MigrateAsync();

            Console.WriteLine("✅ Database migrations applied successfully");

            // Check if we already have data
            var flightCount = await context.Flights.CountAsync();
            if (flightCount > 0)
            {
                Console.WriteLine($"ℹ️ Database already contains {flightCount} flights, skipping seed");
                return; // Database already seeded
            }

            Console.WriteLine("🌱 Seeding database with sample data...");

            // Create sample flights
            var flights = new List<Flight>
            {
                new Flight
                {
                    FlightNumber = "MR101",
                    Origin = "ULN",
                    Destination = "PEK",
                    DepartureTime = DateTime.Now.AddHours(3),
                    ArrivalTime = DateTime.Now.AddHours(5),
                    Status = FlightStatus.CheckingIn,
                    AircraftModel = "Boeing 737-800"
                },
                new Flight
                {
                    FlightNumber = "MR102",
                    Origin = "ULN",
                    Destination = "ICN",
                    DepartureTime = DateTime.Now.AddHours(5),
                    ArrivalTime = DateTime.Now.AddHours(8),
                    Status = FlightStatus.CheckingIn,
                    AircraftModel = "Airbus A320"
                },
                new Flight
                {
                    FlightNumber = "MR103",
                    Origin = "PEK",
                    Destination = "ULN",
                    DepartureTime = DateTime.Now.AddHours(7),
                    ArrivalTime = DateTime.Now.AddHours(9),
                    Status = FlightStatus.Delayed,
                    AircraftModel = "Boeing 737-800"
                }
            };

            await context.Flights.AddRangeAsync(flights);
            await context.SaveChangesAsync();
            Console.WriteLine($"✅ Added {flights.Count} flights");

            // Create sample seats for each flight
            var seats = new List<Seat>();
            foreach (var flight in flights)
            {
                // Business class seats (rows 1-3)
                for (int row = 1; row <= 3; row++)
                {
                    foreach (var col in new[] { "A", "C", "D", "F" })
                    {
                        seats.Add(new Seat
                        {
                            SeatId = $"{flight.FlightNumber}-{row}{col}",
                            FlightNumber = flight.FlightNumber,
                            SeatNumber = $"{row}{col}",
                            Class = SeatClass.Business,
                            Price = 500m,
                            IsAvailable = true
                        });
                    }
                }

                // Economy class seats (rows 10-30)
                for (int row = 10; row <= 30; row++)
                {
                    foreach (var col in new[] { "A", "B", "C", "D", "E", "F" })
                    {
                        seats.Add(new Seat
                        {
                            SeatId = $"{flight.FlightNumber}-{row}{col}",
                            FlightNumber = flight.FlightNumber,
                            SeatNumber = $"{row}{col}",
                            Class = SeatClass.Economy,
                            Price = 200m,
                            IsAvailable = true
                        });
                    }
                }
            }

            await context.Seats.AddRangeAsync(seats);
            await context.SaveChangesAsync();
            Console.WriteLine($"✅ Added {seats.Count} seats");

            // Create sample passengers
            var passengers = new List<Passenger>
            {
                new Passenger
                {
                    PassportNumber = "MN12345678",
                    FirstName = "Батболд",
                    LastName = "Мөнх",
                    Email = "batbold@email.com",
                    Phone = "+976-99112233",
                    DateOfBirth = new DateTime(1985, 5, 15)
                },
                new Passenger
                {
                    PassportNumber = "MN87654321",
                    FirstName = "Сарантуяа",
                    LastName = "Болд",
                    Email = "sarantuya@email.com",
                    Phone = "+976-99445566",
                    DateOfBirth = new DateTime(1990, 8, 22)
                },
                new Passenger
                {
                    PassportNumber = "MN11223344",
                    FirstName = "Төмөр",
                    LastName = "Бат",
                    Email = "tomor@email.com",
                    Phone = "+976-99778899",
                    DateOfBirth = new DateTime(1988, 12, 10)
                }
            };

            await context.Passengers.AddRangeAsync(passengers);
            await context.SaveChangesAsync();
            Console.WriteLine($"✅ Added {passengers.Count} passengers");

            // Create sample bookings
            var bookings = new List<Booking>
            {
                new Booking
                {
                    BookingReference = "BK001",
                    PassportNumber = "MN12345678",
                    FlightNumber = "MR101",
                    BookingDate = DateTime.Now.AddDays(-7),
                    Status = BookingStatus.NotCheckedIn,
                    TotalPrice = 200m
                },
                new Booking
                {
                    BookingReference = "BK002",
                    PassportNumber = "MN87654321",
                    FlightNumber = "MR101",
                    BookingDate = DateTime.Now.AddDays(-5),
                    Status = BookingStatus.NotCheckedIn,
                    TotalPrice = 200m
                },
                new Booking
                {
                    BookingReference = "BK003",
                    PassportNumber = "MN11223344",
                    FlightNumber = "MR102",
                    BookingDate = DateTime.Now.AddDays(-3),
                    Status = BookingStatus.NotCheckedIn,
                    TotalPrice = 200m
                }
            };

            await context.Bookings.AddRangeAsync(bookings);
            await context.SaveChangesAsync();
            Console.WriteLine($"✅ Added {bookings.Count} bookings");

            Console.WriteLine("🎉 Database initialization completed successfully!");
            Console.WriteLine($"📊 Summary:");
            Console.WriteLine($"   - {flights.Count} flights");
            Console.WriteLine($"   - {passengers.Count} passengers");
            Console.WriteLine($"   - {bookings.Count} bookings");
            Console.WriteLine($"   - {seats.Count} seats");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Database initialization failed: {ex.Message}");
            Console.WriteLine($"🔍 Stack trace: {ex.StackTrace}");
            throw;
        }
    }
}