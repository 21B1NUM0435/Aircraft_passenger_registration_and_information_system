using FlightSystem.Server.Models;

namespace FlightSystem.Server.Services;

public interface ICheckInService
{
    Task<Booking?> SearchPassengerAsync(string passportNumber, string flightNumber);
    Task<SeatAssignmentResult> AssignSeatAsync(string bookingReference, string seatId, string staffName);
    Task<List<Booking>> GetFlightPassengersAsync(string flightNumber);
    Task<bool> GenerateBoardingPassAsync(string bookingReference);
}

public class CheckInService : ICheckInService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConcurrencyService _concurrencyService;
    private readonly ILogger<CheckInService> _logger;

    public CheckInService(
        IUnitOfWork unitOfWork,
        IConcurrencyService concurrencyService,
        ILogger<CheckInService> logger)
    {
        _unitOfWork = unitOfWork;
        _concurrencyService = concurrencyService;
        _logger = logger;
    }

    public async Task<Booking?> SearchPassengerAsync(string passportNumber, string flightNumber)
    {
        try
        {
            return await _unitOfWork.Bookings.FindBookingAsync(passportNumber, flightNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching passenger {PassportNumber} on flight {FlightNumber}",
                passportNumber, flightNumber);
            throw;
        }
    }

    public async Task<SeatAssignmentResult> AssignSeatAsync(string bookingReference, string seatId, string staffName)
    {
        try
        {
            return await _concurrencyService.AssignSeatWithConcurrencyCheckAsync(seatId, bookingReference, staffName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning seat {SeatId} to booking {BookingReference}",
                seatId, bookingReference);
            throw;
        }
    }

    public async Task<List<Booking>> GetFlightPassengersAsync(string flightNumber)
    {
        try
        {
            return await _unitOfWork.Bookings.GetBookingsByFlightAsync(flightNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting passengers for flight {FlightNumber}", flightNumber);
            throw;
        }
    }

    public async Task<bool> GenerateBoardingPassAsync(string bookingReference)
    {
        try
        {
            var booking = await _unitOfWork.Bookings.GetBookingWithDetailsAsync(bookingReference);
            if (booking == null || booking.Status != BookingStatus.CheckedIn)
            {
                return false;
            }

            // Generate boarding pass logic here
            var boardingPass = new BoardingPass
            {
                BoardingPassId = $"BP{DateTime.Now:yyyyMMddHHmmss}{Random.Shared.Next(100, 999)}",
                BookingReference = bookingReference,
                Gate = "A1", // This would come from flight gate assignment
                BoardingTime = booking.Flight.DepartureTime.AddMinutes(-30),
                Barcode = GenerateBarcode(bookingReference),
                IssuedAt = DateTime.UtcNow,
                IssuedBy = "System"
            };

            await _unitOfWork.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating boarding pass for booking {BookingReference}", bookingReference);
            return false;
        }
    }

    private string GenerateBarcode(string bookingReference)
    {
        // Simple barcode generation - in production, use proper barcode library
        return $"*{bookingReference}*{DateTime.Now:yyyyMMdd}*";
    }
}