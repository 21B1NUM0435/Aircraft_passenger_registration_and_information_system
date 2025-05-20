using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace FlightManagementSystem.Core.Models
{
    public enum MessageType
    {
        FlightStatusUpdate,
        SeatAssignment,
        CheckInComplete,
        Error,
        Ping
    }

    public class SocketMessage
    {
        [JsonPropertyName("type")]
        public MessageType Type { get; set; }

        [JsonPropertyName("data")]
        public object Data { get; set; } = null!;

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class FlightStatusUpdateMessage
    {
        [JsonPropertyName("flightNumber")]
        public string FlightNumber { get; set; } = string.Empty;

        [JsonPropertyName("newStatus")]
        public FlightStatus NewStatus { get; set; }
    }

    public class SeatAssignmentMessage
    {
        [JsonPropertyName("flightNumber")]
        public string FlightNumber { get; set; } = string.Empty;

        [JsonPropertyName("seatId")]
        public string SeatId { get; set; } = string.Empty;

        [JsonPropertyName("isAssigned")]
        public bool IsAssigned { get; set; }
    }

    public class CheckInCompleteMessage
    {
        [JsonPropertyName("flightNumber")]
        public string FlightNumber { get; set; } = string.Empty;

        [JsonPropertyName("bookingReference")]
        public string BookingReference { get; set; } = string.Empty;

        [JsonPropertyName("passengerName")]
        public string PassengerName { get; set; } = string.Empty;
    }

    public class ErrorMessage
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        public int Code { get; set; }
    }
}
