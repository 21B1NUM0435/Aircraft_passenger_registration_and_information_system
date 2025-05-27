using FlightManagementSystem.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FlightManagementSystem.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Aircraft> Aircraft { get; set; } = null!;
        public DbSet<Flight> Flights { get; set; } = null!;
        public DbSet<Passenger> Passengers { get; set; } = null!;
        public DbSet<Booking> Bookings { get; set; } = null!;
        public DbSet<Seat> Seats { get; set; } = null!;
        public DbSet<SeatAssignment> SeatAssignments { get; set; } = null!;
        public DbSet<BoardingPass> BoardingPasses { get; set; } = null!;
        public DbSet<AirlineStaff> AirlineStaff { get; set; } = null!;
        public DbSet<CheckInCounter> CheckInCounters { get; set; } = null!;
        public DbSet<CheckInRecord> CheckInRecords { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure primary keys
            modelBuilder.Entity<Aircraft>().HasKey(a => a.AircraftId);
            modelBuilder.Entity<Flight>().HasKey(f => f.FlightNumber);
            modelBuilder.Entity<Passenger>().HasKey(p => p.PassengerId);
            modelBuilder.Entity<Booking>().HasKey(b => b.BookingReference);
            modelBuilder.Entity<Seat>().HasKey(s => s.SeatId);
            modelBuilder.Entity<SeatAssignment>().HasKey(sa => sa.AssignmentId);
            modelBuilder.Entity<BoardingPass>().HasKey(bp => bp.BoardingPassId);
            modelBuilder.Entity<AirlineStaff>().HasKey(s => s.StaffId);
            modelBuilder.Entity<CheckInCounter>().HasKey(c => c.CounterId);
            modelBuilder.Entity<CheckInRecord>().HasKey(r => r.CheckInId);

            // Configure relationships
            modelBuilder.Entity<Flight>()
                .HasOne(f => f.Aircraft)
                .WithMany(a => a.Flights)
                .HasForeignKey(f => f.AircraftId);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Passenger)
                .WithMany(p => p.Bookings)
                .HasForeignKey(b => b.PassengerId);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Flight)
                .WithMany(f => f.Bookings)
                .HasForeignKey(b => b.FlightNumber);

            modelBuilder.Entity<Seat>()
                .HasOne(s => s.Aircraft)
                .WithMany(a => a.Seats)
                .HasForeignKey(s => s.AircraftId);

            modelBuilder.Entity<SeatAssignment>()
                .HasOne(sa => sa.Booking)
                .WithMany(b => b.SeatAssignments)
                .HasForeignKey(sa => sa.BookingReference);

            modelBuilder.Entity<SeatAssignment>()
                .HasOne(sa => sa.Seat)
                .WithMany(s => s.SeatAssignments)
                .HasForeignKey(sa => sa.SeatId);

            modelBuilder.Entity<BoardingPass>()
                .HasOne(bp => bp.Booking)
                .WithMany(b => b.BoardingPasses)
                .HasForeignKey(bp => bp.BookingReference);

            modelBuilder.Entity<CheckInCounter>()
                .HasOne(c => c.Staff)
                .WithMany(s => s.CheckInCounters)
                .HasForeignKey(c => c.StaffId);

            modelBuilder.Entity<CheckInRecord>()
                .HasOne(r => r.Booking)
                .WithMany(b => b.CheckInRecords)
                .HasForeignKey(r => r.BookingReference);

            modelBuilder.Entity<CheckInRecord>()
                .HasOne(r => r.Counter)
                .WithMany(c => c.CheckInRecords)
                .HasForeignKey(r => r.CounterId);

            modelBuilder.Entity<CheckInRecord>()
                .HasOne(r => r.Staff)
                .WithMany(s => s.CheckInRecords)
                .HasForeignKey(r => r.StaffId);
        }
    }
}