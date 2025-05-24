using System.ComponentModel.DataAnnotations;

namespace FlightSystem.Server.Models;

public enum BookingStatus
{
    NotCheckedIn,
    CheckedIn,
    Boarding,
    Boarded
}

public class Booking
{
    [Key]
    public string BookingReference { get; set; } = string.Empty;
    
    public string PassportNumber { get; set; } = string.Empty;
    
    public string FlightNumber { get; set; } = string.Empty;
    
    public string? SeatId { get; set; }
    
    public DateTime BookingDate { get; set; }
    
    public BookingStatus Status { get; set; } = BookingStatus.NotCheckedIn;
    
    public decimal TotalPrice { get; set; }
    
    public DateTime? CheckInTime { get; set; }
    
    public string? CheckInStaff { get; set; }
    
    // Navigation properties
    public virtual Passenger Passenger { get; set; } = null!;
    public virtual Flight Flight { get; set; } = null!;
    public virtual Seat? Seat { get; set; }
}