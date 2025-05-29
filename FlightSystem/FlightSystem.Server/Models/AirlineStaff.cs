using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlightSystem.Server.Models;

public class AirlineStaff
{
    [Key]
    [StringLength(10)]
    public string StaffId { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Position { get; set; } = string.Empty;

    [StringLength(50)]
    public string Department { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime HiredDate { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<CheckInCounter> CheckInCounters { get; set; } = new List<CheckInCounter>();
    public virtual ICollection<CheckInRecord> CheckInRecords { get; set; } = new List<CheckInRecord>();
}