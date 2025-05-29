using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlightSystem.Server.Models;

public class Aircraft
{
    [Key]
    [StringLength(10)]
    public string AircraftId { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Model { get; set; } = string.Empty;

    [Range(1, 1000)]
    public int Capacity { get; set; }

    public DateTime ManufacturedDate { get; set; }

    [StringLength(50)]
    public string Manufacturer { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<Flight> Flights { get; set; } = new List<Flight>();
    public virtual ICollection<Seat> SeatConfiguration { get; set; } = new List<Seat>();
}