using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlightSystem.Server.Models;

public enum SeatClass
{
    Economy = 0,
    Business = 1,
    FirstClass = 2
}

public class Seat
{
    [Key]
    [StringLength(20)]
    public string SeatId { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string AircraftId { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string FlightNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(5)]
    public string SeatNumber { get; set; } = string.Empty;

    [Required]
    public SeatClass Class { get; set; } = SeatClass.Economy;

    [Required]
    [Column(TypeName = "decimal(8,2)")]
    public decimal Price { get; set; }

    public bool IsAvailable { get; set; } = true;

    public bool IsWindow { get; set; } = false;
    public bool IsAisle { get; set; } = false;
    public bool IsEmergencyExit { get; set; } = false;

    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Concurrency control
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    // Navigation properties
    public virtual Aircraft Aircraft { get; set; } = null!;
    public virtual Flight Flight { get; set; } = null!;
    public virtual Booking? Booking { get; set; }
}