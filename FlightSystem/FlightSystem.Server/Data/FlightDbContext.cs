using Microsoft.EntityFrameworkCore;
using FlightSystem.Server.Models;

namespace FlightSystem.Server.Data;

public class FlightDbContext : DbContext
{
    public FlightDbContext(DbContextOptions<FlightDbContext> options) : base(options)
    {
    }

    // DbSets for all entities
    public DbSet<Aircraft> Aircraft { get; set; }
    public DbSet<Flight> Flights { get; set; }
    public DbSet<Passenger> Passengers { get; set; }
    public DbSet<Seat> Seats { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<BoardingPass> BoardingPasses { get; set; }
    public DbSet<AirlineStaff> AirlineStaff { get; set; }
    public DbSet<CheckInCounter> CheckInCounters { get; set; }
    public DbSet<CheckInRecord> CheckInRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureAircraft(modelBuilder);
        ConfigureFlight(modelBuilder);
        ConfigurePassenger(modelBuilder);
        ConfigureSeat(modelBuilder);
        ConfigureBooking(modelBuilder);
        ConfigureBoardingPass(modelBuilder);
        ConfigureAirlineStaff(modelBuilder);
        ConfigureCheckInCounter(modelBuilder);
        ConfigureCheckInRecord(modelBuilder);

        // Add indexes for performance
        AddIndexes(modelBuilder);

        // Seed initial data
        SeedData(modelBuilder);
    }

    private void ConfigureAircraft(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Aircraft>(entity =>
        {
            entity.HasKey(e => e.AircraftId);
            entity.Property(e => e.AircraftId).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Model).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Manufacturer).HasMaxLength(50);
            entity.Property(e => e.Capacity).IsRequired();
            entity.Property(e => e.ManufacturedDate).IsRequired();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("datetime('now')");

            // Configure audit trigger for UpdatedAt
            entity.Property(e => e.UpdatedAt).ValueGeneratedOnAddOrUpdate();
        });
    }

    private void ConfigureFlight(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Flight>(entity =>
        {
            entity.HasKey(e => e.FlightNumber);
            entity.Property(e => e.FlightNumber).HasMaxLength(10).IsRequired();
            entity.Property(e => e.AircraftId).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Origin).HasMaxLength(3).IsRequired();
            entity.Property(e => e.Destination).HasMaxLength(3).IsRequired();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("datetime('now')");
            entity.Property(e => e.CreatedBy).HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasMaxLength(50);

            // Relationships
            entity.HasOne(e => e.Aircraft)
                  .WithMany(e => e.Flights)
                  .HasForeignKey(e => e.AircraftId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Concurrency token
            entity.Property(e => e.RowVersion).IsRowVersion();
        });
    }

    private void ConfigurePassenger(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Passenger>(entity =>
        {
            entity.HasKey(e => e.PassportNumber);
            entity.Property(e => e.PassportNumber).HasMaxLength(20).IsRequired();
            entity.Property(e => e.FirstName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Phone).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Gender).HasMaxLength(10);
            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.Nationality).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("datetime('now')");

            // Computed columns are not mapped
            entity.Ignore(e => e.FullName);
            entity.Ignore(e => e.Age);
        });
    }

    private void ConfigureSeat(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Seat>(entity =>
        {
            entity.HasKey(e => e.SeatId);
            entity.Property(e => e.SeatId).HasMaxLength(20).IsRequired();
            entity.Property(e => e.AircraftId).HasMaxLength(10).IsRequired();
            entity.Property(e => e.FlightNumber).HasMaxLength(10).IsRequired();
            entity.Property(e => e.SeatNumber).HasMaxLength(5).IsRequired();
            entity.Property(e => e.Class).HasConversion<int>();
            entity.Property(e => e.Price).HasColumnType("decimal(8,2)");
            entity.Property(e => e.IsAvailable).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("datetime('now')");

            // Relationships
            entity.HasOne(e => e.Aircraft)
                  .WithMany(e => e.SeatConfiguration)
                  .HasForeignKey(e => e.AircraftId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Flight)
                  .WithMany(e => e.Seats)
                  .HasForeignKey(e => e.FlightNumber)
                  .OnDelete(DeleteBehavior.Cascade);

            // Concurrency token
            entity.Property(e => e.RowVersion).IsRowVersion();
        });
    }

    private void ConfigureBooking(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.BookingReference);
            entity.Property(e => e.BookingReference).HasMaxLength(10).IsRequired();
            entity.Property(e => e.PassportNumber).HasMaxLength(20).IsRequired();
            entity.Property(e => e.FlightNumber).HasMaxLength(10).IsRequired();
            entity.Property(e => e.SeatId).HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.PaymentStatus).HasConversion<int>();
            entity.Property(e => e.TotalPrice).HasColumnType("decimal(10,2)");
            entity.Property(e => e.CheckInStaff).HasMaxLength(50);
            entity.Property(e => e.CheckInCounter).HasMaxLength(5);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("datetime('now')");

            // Relationships
            entity.HasOne(e => e.Passenger)
                  .WithMany(e => e.Bookings)
                  .HasForeignKey(e => e.PassportNumber)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Flight)
                  .WithMany(e => e.Bookings)
                  .HasForeignKey(e => e.FlightNumber)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Seat)
                  .WithOne(e => e.Booking)
                  .HasForeignKey<Booking>(e => e.SeatId)
                  .OnDelete(DeleteBehavior.SetNull);

            // Concurrency token
            entity.Property(e => e.RowVersion).IsRowVersion();
        });
    }

    private void ConfigureBoardingPass(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BoardingPass>(entity =>
        {
            entity.HasKey(e => e.BoardingPassId);
            entity.Property(e => e.BoardingPassId).HasMaxLength(15).IsRequired();
            entity.Property(e => e.BookingReference).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Gate).HasMaxLength(5).IsRequired();
            entity.Property(e => e.Barcode).HasMaxLength(100).IsRequired();
            entity.Property(e => e.IssuedBy).HasMaxLength(50);
            entity.Property(e => e.PrintedBy).HasMaxLength(20);
            entity.Property(e => e.IssuedAt).HasDefaultValueSql("datetime('now')");

            // Relationships
            entity.HasOne(e => e.Booking)
                  .WithOne(e => e.BoardingPass)
                  .HasForeignKey<BoardingPass>(e => e.BookingReference)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private void ConfigureAirlineStaff(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AirlineStaff>(entity =>
        {
            entity.HasKey(e => e.StaffId);
            entity.Property(e => e.StaffId).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Position).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Department).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("datetime('now')");

            // Unique constraints
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });
    }

    private void ConfigureCheckInCounter(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CheckInCounter>(entity =>
        {
            entity.HasKey(e => e.CounterId);
            entity.Property(e => e.CounterId).HasMaxLength(5).IsRequired();
            entity.Property(e => e.StaffId).HasMaxLength(10);
            entity.Property(e => e.Terminal).HasMaxLength(5).IsRequired();
            entity.Property(e => e.Location).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("datetime('now')");

            // Relationships
            entity.HasOne(e => e.Staff)
                  .WithMany(e => e.CheckInCounters)
                  .HasForeignKey(e => e.StaffId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private void ConfigureCheckInRecord(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CheckInRecord>(entity =>
        {
            entity.HasKey(e => e.CheckInId);
            entity.Property(e => e.CheckInId).HasMaxLength(15).IsRequired();
            entity.Property(e => e.BookingReference).HasMaxLength(10).IsRequired();
            entity.Property(e => e.CounterId).HasMaxLength(5);
            entity.Property(e => e.StaffId).HasMaxLength(10);
            entity.Property(e => e.CheckInMethod).HasConversion<int>();
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.CheckInTime).HasDefaultValueSql("datetime('now')");

            // Relationships
            entity.HasOne(e => e.Booking)
                  .WithMany(e => e.CheckInRecords)
                  .HasForeignKey(e => e.BookingReference)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Counter)
                  .WithMany(e => e.CheckInRecords)
                  .HasForeignKey(e => e.CounterId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Staff)
                  .WithMany(e => e.CheckInRecords)
                  .HasForeignKey(e => e.StaffId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private void AddIndexes(ModelBuilder modelBuilder)
    {
        // Performance indexes
        modelBuilder.Entity<Flight>()
            .HasIndex(f => f.DepartureTime)
            .HasDatabaseName("IX_Flight_DepartureTime");

        modelBuilder.Entity<Flight>()
            .HasIndex(f => f.Status)
            .HasDatabaseName("IX_Flight_Status");

        modelBuilder.Entity<Booking>()
            .HasIndex(b => new { b.PassportNumber, b.FlightNumber })
            .IsUnique()
            .HasDatabaseName("IX_Booking_Passenger_Flight");

        modelBuilder.Entity<Booking>()
            .HasIndex(b => b.Status)
            .HasDatabaseName("IX_Booking_Status");

        modelBuilder.Entity<Seat>()
            .HasIndex(s => new { s.FlightNumber, s.IsAvailable })
            .HasDatabaseName("IX_Seat_Flight_Available");

        modelBuilder.Entity<Seat>()
            .HasIndex(s => s.SeatNumber)
            .HasDatabaseName("IX_Seat_Number");

        modelBuilder.Entity<Passenger>()
            .HasIndex(p => p.Email)
            .HasDatabaseName("IX_Passenger_Email");

        modelBuilder.Entity<CheckInRecord>()
            .HasIndex(c => c.CheckInTime)
            .HasDatabaseName("IX_CheckInRecord_Time");
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Seed Aircraft
        modelBuilder.Entity<Aircraft>().HasData(
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
            },
            new Aircraft
            {
                AircraftId = "AC003",
                Model = "Boeing 777-300ER",
                Manufacturer = "Boeing",
                Capacity = 350,
                ManufacturedDate = new DateTime(2018, 11, 22),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );

        // Seed Flights
        var baseTime = DateTime.Now.Date.AddHours(8); // Start at 8 AM today

        modelBuilder.Entity<Flight>().HasData(
            new Flight
            {
                FlightNumber = "MR101",
                AircraftId = "AC001",
                Origin = "ULN",
                Destination = "PEK",
                DepartureTime = baseTime.AddHours(3),
                ArrivalTime = baseTime.AddHours(5),
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
                DepartureTime = baseTime.AddHours(5),
                ArrivalTime = baseTime.AddHours(8),
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
                DepartureTime = baseTime.AddHours(7),
                ArrivalTime = baseTime.AddHours(9),
                Status = FlightStatus.Delayed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                UpdatedBy = "System"
            }
        );

        // Seed Passengers
        modelBuilder.Entity<Passenger>().HasData(
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
        );

        // Seed Bookings
        modelBuilder.Entity<Booking>().HasData(
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
        );

        // Seed Staff
        modelBuilder.Entity<AirlineStaff>().HasData(
            new AirlineStaff
            {
                StaffId = "ST001",
                Name = "Админ Систем",
                Username = "admin",
                PasswordHash = "$2a$11$dummy.hash.for.development.only", // In production, use proper hashing
                Position = "System Administrator",
                Department = "IT",
                Email = "admin@flightsystem.com",
                Phone = "+976-99000001",
                IsActive = true,
                HiredDate = new DateTime(2023, 1, 1),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AirlineStaff
            {
                StaffId = "ST002",
                Name = "Бүртгэлийн Ажилтан 1",
                Username = "checkin1",
                PasswordHash = "$2a$11$dummy.hash.for.development.only",
                Position = "Check-in Agent",
                Department = "Ground Services",
                Email = "checkin1@flightsystem.com",
                Phone = "+976-99000002",
                IsActive = true,
                HiredDate = new DateTime(2023, 6, 1),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );

        // Seed Check-in Counters
        modelBuilder.Entity<CheckInCounter>().HasData(
            new CheckInCounter
            {
                CounterId = "C001",
                StaffId = "ST002",
                Terminal = "T1",
                Location = "Terminal 1, Gate A1-A5",
                Status = CounterStatus.Open,
                OpenedAt = DateTime.Now.AddHours(-2),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new CheckInCounter
            {
                CounterId = "C002",
                Terminal = "T1",
                Location = "Terminal 1, Gate A6-A10",
                Status = CounterStatus.Closed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );
    }

    // Override SaveChanges to update audit fields
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateAuditFields();
        return base.SaveChanges();
    }

    private void UpdateAuditFields()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Entity.GetType().GetProperty("UpdatedAt") != null)
            {
                entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
            }

            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.GetType().GetProperty("CreatedAt") != null)
                {
                    entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
                }
            }

            // Set UpdatedBy field if available (you can get current user from context)
            if (entry.Entity.GetType().GetProperty("UpdatedBy") != null)
            {
                // In a real application, get current user from HttpContext or authentication
                entry.Property("UpdatedBy").CurrentValue = "System"; // Default value
            }
        }
    }
}