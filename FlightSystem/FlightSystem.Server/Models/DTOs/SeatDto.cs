using FlightSystem.Server.Models;

namespace FlightSystem.Server.Models.DTOs;

public class SeatDto
{
    public string SeatId { get; set; } = string.Empty;
    public string SeatNumber { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsWindow { get; set; }
    public bool IsAisle { get; set; }

    public static SeatDto FromSeat(Seat seat)
    {
        return new SeatDto
        {
            SeatId = seat.SeatId,
            SeatNumber = seat.SeatNumber,
            Class = seat.Class.ToString(),
            Price = seat.Price,
            IsWindow = seat.IsWindow,
            IsAisle = seat.IsAisle
        };
    }
}