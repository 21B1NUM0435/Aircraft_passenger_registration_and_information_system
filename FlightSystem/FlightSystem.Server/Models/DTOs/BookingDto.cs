using FlightSystem.Server.Models;

namespace FlightSystem.Server.Models.DTOs;

public class BookingDto
{
    public string BookingReference { get; set; } = string.Empty;
    public string PassengerName { get; set; } = string.Empty;
    public string FlightNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AssignedSeat { get; set; }
    public DateTime? CheckInTime { get; set; }

    public static BookingDto FromBooking(Booking booking)
    {
        return new BookingDto
        {
            BookingReference = booking.BookingReference,
            PassengerName = booking.Passenger?.FullName ?? "Unknown",
            FlightNumber = booking.FlightNumber,
            Status = booking.Status.ToString(),
            AssignedSeat = booking.Seat?.SeatNumber,
            CheckInTime = booking.CheckInTime
        };
    }
}