using System.Text.Json.Serialization;

namespace FlightManagementSystem.Web.Models.Api
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
    }

    public class FlightDetailDto : FlightDto
    {
        [JsonPropertyName("aircraftId")]
        public string AircraftId { get; set; } = string.Empty;
    }
}