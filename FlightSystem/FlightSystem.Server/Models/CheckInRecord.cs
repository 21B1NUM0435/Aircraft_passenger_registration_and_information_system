using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlightSystem.Server.Models;

public enum CheckInMethod
{
    Counter = 0,
    Online = 1,
    Mobile = 2,
    Kiosk = 3
}

public class CheckInRecord
{
    [Key]
    [StringLength(15)]
    public string CheckInId { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string BookingReference { get; set; } = string.Empty;

    [StringLength(5)]
    public string? CounterId { get; set; }

    [StringLength(10)]
    public string? StaffId { get; set; }

    [Required]
    public DateTime CheckInTime { get; set; } = DateTime.UtcNow;

    [Required]
    public CheckInMethod CheckInMethod { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    // Navigation properties
    public virtual Booking Booking { get; set; } = null!;
    public virtual CheckInCounter? Counter { get; set; }
    public virtual AirlineStaff? Staff { get; set; }
}