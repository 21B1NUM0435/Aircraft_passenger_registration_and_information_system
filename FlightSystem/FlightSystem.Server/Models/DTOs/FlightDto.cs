using FlightSystem.Server.Models;

namespace FlightSystem.Server.Models.DTOs;

public class FlightDto
{
    public string FlightNumber { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AircraftModel { get; set; } = string.Empty;
    public int AvailableSeats { get; set; }
    public int TotalBookings { get; set; }
    public int CheckedInPassengers { get; set; }

    public static FlightDto FromFlight(Flight flight)
    {
        return new FlightDto
        {
            FlightNumber = flight.FlightNumber,
            Origin = flight.Origin,
            Destination = flight.Destination,
            DepartureTime = flight.DepartureTime,
            ArrivalTime = flight.ArrivalTime,
            Status = flight.Status.ToString(),
            AircraftModel = flight.Aircraft?.Model ?? "Unknown",
            AvailableSeats = flight.Seats?.Count(s => s.IsAvailable) ?? 0,
            TotalBookings = flight.Bookings?.Count ?? 0,
            CheckedInPassengers = flight.Bookings?.Count(b => b.Status == BookingStatus.CheckedIn) ?? 0
        };
    }
}