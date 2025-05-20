using System.ComponentModel.DataAnnotations;

namespace FlightManagementSystem.Web.Models.Api
{
    public class BookingDto
    {
        public string BookingReference { get; set; } = string.Empty;
        public string PassengerId { get; set; } = string.Empty;
        public string FlightNumber { get; set; } = string.Empty;
        public string PassengerName { get; set; } = string.Empty;
        public bool CheckedIn { get; set; }
    }

    public class SeatDto
    {
        public string SeatId { get; set; } = string.Empty;
        public string SeatNumber { get; set; } = string.Empty;
        public string SeatClass { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class CheckInRequestDto
    {
        [Required]
        public string BookingReference { get; set; } = string.Empty;

        [Required]
        public string FlightNumber { get; set; } = string.Empty;

        [Required]
        public string SeatId { get; set; } = string.Empty;

        [Required]
        public string StaffId { get; set; } = string.Empty;

        [Required]
        public string CounterId { get; set; } = string.Empty;

        public string PassengerName { get; set; } = string.Empty;
    }

    public class CheckInResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? BoardingPassId { get; set; }
        public byte[] BoardingPassPdf { get; set; } = Array.Empty<byte>();
    }
}