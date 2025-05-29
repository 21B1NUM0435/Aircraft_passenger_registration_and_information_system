using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlightSystem.Server.Models;

public class Passenger
{
    [Key]
    [StringLength(20)]
    public string PassportNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required]
    public DateTime DateOfBirth { get; set; }

    [StringLength(10)]
    public string Gender { get; set; } = string.Empty;

    [StringLength(255)]
    public string Address { get; set; } = string.Empty;

    public DateTime PassportExpiryDate { get; set; }

    [StringLength(50)]
    public string Nationality { get; set; } = string.Empty;

    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Computed property
    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";

    [NotMapped]
    public int Age => DateTime.Today.Year - DateOfBirth.Year -
        (DateTime.Today.DayOfYear < DateOfBirth.DayOfYear ? 1 : 0);

    // Navigation properties
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}