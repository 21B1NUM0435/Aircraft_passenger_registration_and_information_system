using System.ComponentModel.DataAnnotations;

namespace FlightSystem.Server.Models;

public enum FlightStatus
{
    CheckingIn = 0,     // Бүртгэж байна
    Boarding = 1,       // Онгоцонд сууж байна
    Departed = 2,       // Ниссэн
    Delayed = 3,        // Хойшилсон
    Cancelled = 4       // Цуцалсан
}

public class Flight
{
    [Key]
    [StringLength(10)]
    public string FlightNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(3)]
    public string Origin { get; set; } = string.Empty;

    [Required]
    [StringLength(3)]
    public string Destination { get; set; } = string.Empty;

    [Required]
    public DateTime DepartureTime { get; set; }

    [Required]
    public DateTime ArrivalTime { get; set; }

    [Required]
    public FlightStatus Status { get; set; } = FlightStatus.CheckingIn;

    [Required]
    [StringLength(10)]
    public string AircraftId { get; set; } = string.Empty;

    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;

    // Concurrency control
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    // Navigation properties
    public virtual Aircraft Aircraft { get; set; } = null!;
    public virtual ICollection<Seat> Seats { get; set; } = new List<Seat>();
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}