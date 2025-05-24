using System.ComponentModel.DataAnnotations;

namespace FlightSystem.Server.Models;

public class Passenger
{
    [Key]
    public string PassportNumber { get; set; } = string.Empty;
    
    public string FirstName { get; set; } = string.Empty;
    
    public string LastName { get; set; } = string.Empty;
    
    public string Email { get; set; } = string.Empty;
    
    public string Phone { get; set; } = string.Empty;
    
    public DateTime DateOfBirth { get; set; }
    
    // Full name for convenience
    public string FullName => $"{FirstName} {LastName}";
    
    // Navigation properties
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}