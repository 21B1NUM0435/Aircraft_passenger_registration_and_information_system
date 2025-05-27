using System.Text.Json.Serialization;

namespace FlightManagementSystem.WinApp.Models
{
    public class FlightDto
    {
        [JsonPropertyName("flightNumber")]
        public string FlightNumber { get; set; } = string.Empty;

        [JsonPropertyName("origin")]
        public string Origin { get; set; } = string.Empty;

        [JsonPropertyName("destination")]
        public string Destination { get; set; } = string.Empty;

        [JsonPropertyName("departureTime")]
        public DateTime DepartureTime { get; set; }

        [JsonPropertyName("arrivalTime")]
        public DateTime ArrivalTime { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("aircraftModel")]
        public string AircraftModel { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{FlightNumber} - {Origin} to {Destination} - {DepartureTime:HH:mm} - {Status}";
        }
    }

    public class BookingDto
    {
        [JsonPropertyName("bookingReference")]
        public string BookingReference { get; set; } = string.Empty;

        [JsonPropertyName("passengerId")]
        public string PassengerId { get; set; } = string.Empty;

        [JsonPropertyName("flightNumber")]
        public string FlightNumber { get; set; } = string.Empty;

        [JsonPropertyName("passengerName")]
        public string PassengerName { get; set; } = string.Empty;

        [JsonPropertyName("checkedIn")]
        public bool CheckedIn { get; set; }
    }

    public class SeatDto
    {
        [JsonPropertyName("seatId")]
        public string SeatId { get; set; } = string.Empty;

        [JsonPropertyName("seatNumber")]
        public string SeatNumber { get; set; } = string.Empty;

        [JsonPropertyName("seatClass")]
        public string SeatClass { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        public override string ToString()
        {
            return $"{SeatNumber} - {SeatClass}";
        }
    }

    public class CheckInRequestDto
    {
        [JsonPropertyName("bookingReference")]
        public string BookingReference { get; set; } = string.Empty;

        [JsonPropertyName("flightNumber")]
        public string FlightNumber { get; set; } = string.Empty;

        [JsonPropertyName("seatId")]
        public string SeatId { get; set; } = string.Empty;

        [JsonPropertyName("staffId")]
        public string StaffId { get; set; } = string.Empty;

        [JsonPropertyName("counterId")]
        public string CounterId { get; set; } = string.Empty;

        [JsonPropertyName("passengerName")]
        public string PassengerName { get; set; } = string.Empty;
    }

    public class CheckInResponseDto
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("boardingPassId")]
        public string? BoardingPassId { get; set; }

        [JsonPropertyName("boardingPassPdf")]
        public byte[]? BoardingPassPdf { get; set; }
    }

    public class UpdateFlightStatusDto
    {
        [JsonPropertyName("newStatus")]
        public string NewStatus { get; set; } = string.Empty;
    }

    public class ErrorResponseDto
    {
        [JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;
    }
}