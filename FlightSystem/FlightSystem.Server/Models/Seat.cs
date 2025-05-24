using System.ComponentModel.DataAnnotations;

namespace FlightSystem.Server.Models;

public enum SeatClass
{
    Economy,
    Business,
    FirstClass
}

public class Seat
{
    [Key]
    public string SeatId { get; set; } = string.Empty;
    
    public string FlightNumber { get; set; } = string.Empty;
    
    public string SeatNumber { get; set; } = string.Empty;
    
    public SeatClass Class { get; set; } = SeatClass.Economy;
    
    public decimal Price { get; set; }
    
    public bool IsAvailable { get; set; } = true;
    
    // Navigation properties
    public virtual Flight Flight { get; set; } = null!;
    public virtual Booking? Booking { get; set; }
}