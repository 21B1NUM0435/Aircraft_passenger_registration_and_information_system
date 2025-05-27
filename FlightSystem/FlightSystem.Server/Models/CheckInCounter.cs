using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlightSystem.Server.Models;

public enum CounterStatus
{
    Closed = 0,
    Open = 1,
    Maintenance = 2
}

public class CheckInCounter
{
    [Key]
    [StringLength(5)]
    public string CounterId { get; set; } = string.Empty;

    [StringLength(10)]
    public string? StaffId { get; set; }

    [Required]
    [StringLength(5)]
    public string Terminal { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Location { get; set; } = string.Empty;

    [Required]
    public CounterStatus Status { get; set; } = CounterStatus.Closed;

    public DateTime? OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual AirlineStaff? Staff { get; set; }
    public virtual ICollection<CheckInRecord> CheckInRecords { get; set; } = new List<CheckInRecord>();
}