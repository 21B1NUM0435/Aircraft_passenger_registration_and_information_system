using System.ComponentModel.DataAnnotations;

namespace FlightSystem.Server.Models;

public enum FlightStatus
{
    CheckingIn,     // Бүртгэж байна
    Boarding,       // Онгоцонд сууж байна
    Departed,       // Ниссэн
    Delayed,        // Хойшилсон
    Cancelled       // Цуцалсан
}

public class Flight
{
    [Key]
    public string FlightNumber { get; set; } = string.Empty;
    
    public string Origin { get; set; } = string.Empty;
    
    public string Destination { get; set; } = string.Empty;
    
    public DateTime DepartureTime { get; set; }
    
    public DateTime ArrivalTime { get; set; }
    
    public FlightStatus Status { get; set; } = FlightStatus.CheckingIn;
    
    public string AircraftModel { get; set; } = string.Empty;
    
    // Navigation properties
    public virtual ICollection<Seat> Seats { get; set; } = new List<Seat>();
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}