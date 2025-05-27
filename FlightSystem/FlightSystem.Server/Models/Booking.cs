using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlightSystem.Server.Models;

public enum BookingStatus
{
    NotCheckedIn = 0,
    CheckedIn = 1,
    Boarding = 2,
    Boarded = 3,
    Cancelled = 4
}

public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Refunded = 2,
    Failed = 3
}

public class Booking
{
    [Key]
    [StringLength(10)]
    public string BookingReference { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string PassportNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string FlightNumber { get; set; } = string.Empty;

    [StringLength(20)]
    public string? SeatId { get; set; }

    [Required]
    public DateTime BookingDate { get; set; }

    [Required]
    public BookingStatus Status { get; set; } = BookingStatus.NotCheckedIn;

    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalPrice { get; set; }

    public DateTime? CheckInTime { get; set; }

    [StringLength(50)]
    public string? CheckInStaff { get; set; }

    [StringLength(5)]
    public string? CheckInCounter { get; set; }

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Concurrency control
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    // Navigation properties
    public virtual Passenger Passenger { get; set; } = null!;
    public virtual Flight Flight { get; set; } = null!;
    public virtual Seat? Seat { get; set; }
    public virtual BoardingPass? BoardingPass { get; set; }
    public virtual ICollection<CheckInRecord> CheckInRecords { get; set; } = new List<CheckInRecord>();
}