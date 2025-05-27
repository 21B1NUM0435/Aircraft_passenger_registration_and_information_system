using FlightSystem.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace FlightSystem.Server.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(FlightDbContext context)
    {
        try
        {
            Console.WriteLine("🔨 Checking database initialization...");

            // Check if we already have data
            var flightCount = await context.Flights.CountAsync();
            var seatCount = await context.Seats.CountAsync();

            Console.WriteLine($"ℹ️ Current database state: {flightCount} flights, {seatCount} seats");

            if (flightCount > 0 && seatCount > 0)
            {
                Console.WriteLine($"ℹ️ Database already initialized, skipping seed");
                return;
            }

            Console.WriteLine("🌱 Seeding database with sample data...");

            // Create sample aircraft first
            if (!await context.Aircraft.AnyAsync())
            {
                var aircraft = new List<Aircraft>
                {
                    new Aircraft
                    {
                        AircraftId = "AC001",
                        Model = "Boeing 737-800",
                        Manufacturer = "Boeing",
                        Capacity = 189,
                        ManufacturedDate = new DateTime(2020, 1, 15),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Aircraft
                    {
                        AircraftId = "AC002",
                        Model = "Airbus A320",
                        Manufacturer = "Airbus",
                        Capacity = 180,
                        ManufacturedDate = new DateTime(2019, 6, 10),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    }
                };

                await context.Aircraft.AddRangeAsync(aircraft);
                await context.SaveChangesAsync();
                Console.WriteLine($"✅ Added {aircraft.Count} aircraft");
            }

            // Create sample flights
            if (!await context.Flights.AnyAsync())
            {
                var flights = new List<Flight>
                {
                    new Flight
                    {
                        FlightNumber = "MR101",
                        AircraftId = "AC001",
                        Origin = "ULN",
                        Destination = "PEK",
                        DepartureTime = DateTime.Now.AddHours(3),
                        ArrivalTime = DateTime.Now.AddHours(5),
                        Status = FlightStatus.CheckingIn,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = "System",
                        UpdatedBy = "System"
                    },
                    new Flight
                    {
                        FlightNumber = "MR102",
                        AircraftId = "AC002",
                        Origin = "ULN",
                        Destination = "ICN",
                        DepartureTime = DateTime.Now.AddHours(5),
                        ArrivalTime = DateTime.Now.AddHours(8),
                        Status = FlightStatus.CheckingIn,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = "System",
                        UpdatedBy = "System"
                    },
                    new Flight
                    {
                        FlightNumber = "MR103",
                        AircraftId = "AC001",
                        Origin = "PEK",
                        Destination = "ULN",
                        DepartureTime = DateTime.Now.AddHours(7),
                        ArrivalTime = DateTime.Now.AddHours(9),
                        Status = FlightStatus.Delayed,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = "System",
                        UpdatedBy = "System"
                    }
                };

                await context.Flights.AddRangeAsync(flights);
                await context.SaveChangesAsync();
                Console.WriteLine($"✅ Added {flights.Count} flights");
            }

            // CREATE SEATS - This is the important part that was missing!
            if (!await context.Seats.AnyAsync())
            {
                Console.WriteLine("🪑 Creating seats for all flights...");

                var flights = await context.Flights.ToListAsync();
                var seats = new List<Seat>();

                foreach (var flight in flights)
                {
                    Console.WriteLine($"🪑 Creating seats for flight {flight.FlightNumber}...");

                    // Business class seats (rows 1-3, 4 seats per row)
                    for (int row = 1; row <= 3; row++)
                    {
                        foreach (var col in new[] { "A", "C", "D", "F" })
                        {
                            seats.Add(new Seat
                            {
                                SeatId = $"{flight.FlightNumber}-{row}{col}",
                                AircraftId = flight.AircraftId,
                                FlightNumber = flight.FlightNumber,
                                SeatNumber = $"{row}{col}",
                                Class = SeatClass.Business,
                                Price = 500m,
                                IsAvailable = true,
                                IsWindow = col == "A" || col == "F",
                                IsAisle = col == "C" || col == "D",
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            });
                        }
                    }

                    // Economy class seats (rows 10-25, 6 seats per row)
                    for (int row = 10; row <= 25; row++)
                    {
                        foreach (var col in new[] { "A", "B", "C", "D", "E", "F" })
                        {
                            seats.Add(new Seat
                            {
                                SeatId = $"{flight.FlightNumber}-{row}{col}",
                                AircraftId = flight.AircraftId,
                                FlightNumber = flight.FlightNumber,
                                SeatNumber = $"{row}{col}",
                                Class = SeatClass.Economy,
                                Price = 200m,
                                IsAvailable = true,
                                IsWindow = col == "A" || col == "F",
                                IsAisle = col == "C" || col == "D",
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            });
                        }
                    }
                }

                Console.WriteLine($"🪑 Prepared {seats.Count} seats, adding to database...");

                // Add seats in batches to avoid issues
                var batchSize = 100;
                for (int i = 0; i < seats.Count; i += batchSize)
                {
                    var batch = seats.Skip(i).Take(batchSize).ToList();
                    await context.Seats.AddRangeAsync(batch);
                    await context.SaveChangesAsync();
                    Console.WriteLine($"✅ Added batch {(i / batchSize) + 1}: {batch.Count} seats");
                }

                Console.WriteLine($"✅ Successfully added {seats.Count} total seats");
            }

            // Create sample passengers
            if (!await context.Passengers.AnyAsync())
            {
                var passengers = new List<Passenger>
                {
                    new Passenger
                    {
                        PassportNumber = "MN12345678",
                        FirstName = "Батболд",
                        LastName = "Мөнх",
                        Email = "batbold@email.com",
                        Phone = "+976-99112233",
                        DateOfBirth = new DateTime(1985, 5, 15),
                        Gender = "Male",
                        Nationality = "Mongolian",
                        PassportExpiryDate = new DateTime(2030, 5, 15),
                        Address = "Ulaanbaatar, Mongolia",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Passenger
                    {
                        PassportNumber = "MN87654321",
                        FirstName = "Сарантуяа",
                        LastName = "Болд",
                        Email = "sarantuya@email.com",
                        Phone = "+976-99445566",
                        DateOfBirth = new DateTime(1990, 8, 22),
                        Gender = "Female",
                        Nationality = "Mongolian",
                        PassportExpiryDate = new DateTime(2029, 8, 22),
                        Address = "Ulaanbaatar, Mongolia",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Passenger
                    {
                        PassportNumber = "MN11223344",
                        FirstName = "Төмөр",
                        LastName = "Бат",
                        Email = "tomor@email.com",
                        Phone = "+976-99778899",
                        DateOfBirth = new DateTime(1988, 12, 10),
                        Gender = "Male",
                        Nationality = "Mongolian",
                        PassportExpiryDate = new DateTime(2031, 12, 10),
                        Address = "Darkhan, Mongolia",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    }
                };

                await context.Passengers.AddRangeAsync(passengers);
                await context.SaveChangesAsync();
                Console.WriteLine($"✅ Added {passengers.Count} passengers");
            }

            // Create sample bookings
            if (!await context.Bookings.AnyAsync())
            {
                var bookings = new List<Booking>
                {
                    new Booking
                    {
                        BookingReference = "BK001",
                        PassportNumber = "MN12345678",
                        FlightNumber = "MR101",
                        BookingDate = DateTime.Now.AddDays(-7),
                        Status = BookingStatus.NotCheckedIn,
                        PaymentStatus = PaymentStatus.Paid,
                        TotalPrice = 200m,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Booking
                    {
                        BookingReference = "BK002",
                        PassportNumber = "MN87654321",
                        FlightNumber = "MR101",
                        BookingDate = DateTime.Now.AddDays(-5),
                        Status = BookingStatus.NotCheckedIn,
                        PaymentStatus = PaymentStatus.Paid,
                        TotalPrice = 200m,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Booking
                    {
                        BookingReference = "BK003",
                        PassportNumber = "MN11223344",
                        FlightNumber = "MR102",
                        BookingDate = DateTime.Now.AddDays(-3),
                        Status = BookingStatus.NotCheckedIn,
                        PaymentStatus = PaymentStatus.Paid,
                        TotalPrice = 200m,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    }
                };

                await context.Bookings.AddRangeAsync(bookings);
                await context.SaveChangesAsync();
                Console.WriteLine($"✅ Added {bookings.Count} bookings");
            }

            // Final verification
            var finalStats = new
            {
                Aircraft = await context.Aircraft.CountAsync(),
                Flights = await context.Flights.CountAsync(),
                Passengers = await context.Passengers.CountAsync(),
                Bookings = await context.Bookings.CountAsync(),
                Seats = await context.Seats.CountAsync()
            };

            Console.WriteLine("🎉 Database initialization completed successfully!");
            Console.WriteLine($"📊 Final Summary:");
            Console.WriteLine($"   - {finalStats.Aircraft} aircraft");
            Console.WriteLine($"   - {finalStats.Flights} flights");
            Console.WriteLine($"   - {finalStats.Passengers} passengers");
            Console.WriteLine($"   - {finalStats.Bookings} bookings");
            Console.WriteLine($"   - {finalStats.Seats} seats");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Database initialization failed: {ex.Message}");
            Console.WriteLine($"🔍 Stack trace: {ex.StackTrace}");
            throw;
        }
    }
}