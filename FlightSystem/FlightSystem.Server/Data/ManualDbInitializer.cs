using Microsoft.Data.Sqlite;
using FlightSystem.Server.Models;

namespace FlightSystem.Server.Data;

public static class ManualDbInitializer
{
    public static async Task InitializeAsync(string connectionString)
    {
        try
        {
            Console.WriteLine("🔨 Creating database manually...");

            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            // Create tables manually
            var createTablesCommand = connection.CreateCommand();
            createTablesCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS Flights (
                    FlightNumber TEXT PRIMARY KEY,
                    Origin TEXT NOT NULL,
                    Destination TEXT NOT NULL,
                    DepartureTime TEXT NOT NULL,
                    ArrivalTime TEXT NOT NULL,
                    Status INTEGER NOT NULL,
                    AircraftModel TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Passengers (
                    PassportNumber TEXT PRIMARY KEY,
                    FirstName TEXT NOT NULL,
                    LastName TEXT NOT NULL,
                    Email TEXT NOT NULL,
                    Phone TEXT NOT NULL,
                    DateOfBirth TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Seats (
                    SeatId TEXT PRIMARY KEY,
                    FlightNumber TEXT NOT NULL,
                    SeatNumber TEXT NOT NULL,
                    Class INTEGER NOT NULL,
                    Price REAL NOT NULL,
                    IsAvailable INTEGER NOT NULL,
                    FOREIGN KEY (FlightNumber) REFERENCES Flights (FlightNumber)
                );

                CREATE TABLE IF NOT EXISTS Bookings (
                    BookingReference TEXT PRIMARY KEY,
                    PassportNumber TEXT NOT NULL,
                    FlightNumber TEXT NOT NULL,
                    SeatId TEXT,
                    BookingDate TEXT NOT NULL,
                    Status INTEGER NOT NULL,
                    TotalPrice REAL NOT NULL,
                    CheckInTime TEXT,
                    CheckInStaff TEXT,
                    FOREIGN KEY (PassportNumber) REFERENCES Passengers (PassportNumber),
                    FOREIGN KEY (FlightNumber) REFERENCES Flights (FlightNumber),
                    FOREIGN KEY (SeatId) REFERENCES Seats (SeatId)
                );
            ";

            await createTablesCommand.ExecuteNonQueryAsync();
            Console.WriteLine("✅ Database tables created successfully");

            // Check if data exists
            var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT COUNT(*) FROM Flights";
            var count = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());

            if (count > 0)
            {
                Console.WriteLine($"ℹ️ Database already contains {count} flights, skipping seed");
                return;
            }

            Console.WriteLine("🌱 Seeding database with sample data...");

            // Insert sample data
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT INTO Flights (FlightNumber, Origin, Destination, DepartureTime, ArrivalTime, Status, AircraftModel) VALUES
                ('MR101', 'ULN', 'PEK', @dep1, @arr1, 0, 'Boeing 737-800'),
                ('MR102', 'ULN', 'ICN', @dep2, @arr2, 0, 'Airbus A320'),
                ('MR103', 'PEK', 'ULN', @dep3, @arr3, 3, 'Boeing 737-800');

                INSERT INTO Passengers (PassportNumber, FirstName, LastName, Email, Phone, DateOfBirth) VALUES
                ('MN12345678', 'Батболд', 'Мөнх', 'batbold@email.com', '+976-99112233', '1985-05-15'),
                ('MN87654321', 'Сарантуяа', 'Болд', 'sarantuya@email.com', '+976-99445566', '1990-08-22'),
                ('MN11223344', 'Төмөр', 'Бат', 'tomor@email.com', '+976-99778899', '1988-12-10');

                INSERT INTO Bookings (BookingReference, PassportNumber, FlightNumber, BookingDate, Status, TotalPrice) VALUES
                ('BK001', 'MN12345678', 'MR101', @book1, 0, 200.00),
                ('BK002', 'MN87654321', 'MR101', @book2, 0, 200.00),
                ('BK003', 'MN11223344', 'MR102', @book3, 0, 200.00);
            ";

            insertCommand.Parameters.AddWithValue("@dep1", DateTime.Now.AddHours(3).ToString("yyyy-MM-dd HH:mm:ss"));
            insertCommand.Parameters.AddWithValue("@arr1", DateTime.Now.AddHours(5).ToString("yyyy-MM-dd HH:mm:ss"));
            insertCommand.Parameters.AddWithValue("@dep2", DateTime.Now.AddHours(5).ToString("yyyy-MM-dd HH:mm:ss"));
            insertCommand.Parameters.AddWithValue("@arr2", DateTime.Now.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss"));
            insertCommand.Parameters.AddWithValue("@dep3", DateTime.Now.AddHours(7).ToString("yyyy-MM-dd HH:mm:ss"));
            insertCommand.Parameters.AddWithValue("@arr3", DateTime.Now.AddHours(9).ToString("yyyy-MM-dd HH:mm:ss"));
            insertCommand.Parameters.AddWithValue("@book1", DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd HH:mm:ss"));
            insertCommand.Parameters.AddWithValue("@book2", DateTime.Now.AddDays(-5).ToString("yyyy-MM-dd HH:mm:ss"));
            insertCommand.Parameters.AddWithValue("@book3", DateTime.Now.AddDays(-3).ToString("yyyy-MM-dd HH:mm:ss"));

            await insertCommand.ExecuteNonQueryAsync();

            // Insert seats
            var flights = new[] { "MR101", "MR102", "MR103" };
            foreach (var flightNumber in flights)
            {
                // Business seats
                for (int row = 1; row <= 3; row++)
                {
                    foreach (var col in new[] { "A", "C", "D", "F" })
                    {
                        var seatCommand = connection.CreateCommand();
                        seatCommand.CommandText = "INSERT INTO Seats (SeatId, FlightNumber, SeatNumber, Class, Price, IsAvailable) VALUES (@id, @flight, @number, @class, @price, @available)";
                        seatCommand.Parameters.AddWithValue("@id", $"{flightNumber}-{row}{col}");
                        seatCommand.Parameters.AddWithValue("@flight", flightNumber);
                        seatCommand.Parameters.AddWithValue("@number", $"{row}{col}");
                        seatCommand.Parameters.AddWithValue("@class", 1); // Business
                        seatCommand.Parameters.AddWithValue("@price", 500.00);
                        seatCommand.Parameters.AddWithValue("@available", 1);
                        await seatCommand.ExecuteNonQueryAsync();
                    }
                }

                // Economy seats
                for (int row = 10; row <= 30; row++)
                {
                    foreach (var col in new[] { "A", "B", "C", "D", "E", "F" })
                    {
                        var seatCommand = connection.CreateCommand();
                        seatCommand.CommandText = "INSERT INTO Seats (SeatId, FlightNumber, SeatNumber, Class, Price, IsAvailable) VALUES (@id, @flight, @number, @class, @price, @available)";
                        seatCommand.Parameters.AddWithValue("@id", $"{flightNumber}-{row}{col}");
                        seatCommand.Parameters.AddWithValue("@flight", flightNumber);
                        seatCommand.Parameters.AddWithValue("@number", $"{row}{col}");
                        seatCommand.Parameters.AddWithValue("@class", 0); // Economy
                        seatCommand.Parameters.AddWithValue("@price", 200.00);
                        seatCommand.Parameters.AddWithValue("@available", 1);
                        await seatCommand.ExecuteNonQueryAsync();
                    }
                }
            }

            Console.WriteLine("🎉 Database initialization completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Manual database initialization failed: {ex.Message}");
            throw;
        }
    }
}