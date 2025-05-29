using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlightSystem.Server.Models;



public class BoardingPass
{
    [Key]
    [StringLength(15)]
    public string BoardingPassId { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string BookingReference { get; set; } = string.Empty;

    [Required]
    [StringLength(5)]
    public string Gate { get; set; } = string.Empty;

    [Required]
    public DateTime BoardingTime { get; set; }

    [Required]
    [StringLength(100)]
    public string Barcode { get; set; } = string.Empty;

    [Required]
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    [StringLength(50)]
    public string IssuedBy { get; set; } = string.Empty;

    public bool IsPrinted { get; set; } = false;
    public DateTime? PrintedAt { get; set; }

    [StringLength(20)]
    public string? PrintedBy { get; set; }

    // Navigation properties
    public virtual Booking Booking { get; set; } = null!;
}