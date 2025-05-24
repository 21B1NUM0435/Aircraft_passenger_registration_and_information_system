using Microsoft.EntityFrameworkCore;
using FlightSystem.Server.Models;

namespace FlightSystem.Server.Data;

public class FlightDbContext : DbContext
{
    public FlightDbContext(DbContextOptions<FlightDbContext> options) : base(options)
    {
    }

    public DbSet<Flight> Flights { get; set; }
    public DbSet<Passenger> Passengers { get; set; }
    public DbSet<Seat> Seats { get; set; }
    public DbSet<Booking> Bookings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Flight configuration
        modelBuilder.Entity<Flight>(entity =>
        {
            entity.HasKey(e => e.FlightNumber);
            entity.Property(e => e.FlightNumber).HasMaxLength(10);
            entity.Property(e => e.Origin).HasMaxLength(3);
            entity.Property(e => e.Destination).HasMaxLength(3);
            entity.Property(e => e.AircraftModel).HasMaxLength(50);
        });

        // Passenger configuration
        modelBuilder.Entity<Passenger>(entity =>
        {
            entity.HasKey(e => e.PassportNumber);
            entity.Property(e => e.PassportNumber).HasMaxLength(20);
            entity.Property(e => e.FirstName).HasMaxLength(50);
            entity.Property(e => e.LastName).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(20);
        });

        // Seat configuration
        modelBuilder.Entity<Seat>(entity =>
        {
            entity.HasKey(e => e.SeatId);
            entity.Property(e => e.SeatId).HasMaxLength(20);
            entity.Property(e => e.SeatNumber).HasMaxLength(5);
            entity.HasOne(e => e.Flight)
                  .WithMany(e => e.Seats)
                  .HasForeignKey(e => e.FlightNumber)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Booking configuration
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.BookingReference);
            entity.Property(e => e.BookingReference).HasMaxLength(10);
            
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
        });
    }
}