using System.Text.Json.Serialization;

namespace FlightManagementSystem.Web.Models.Api
{
    public class UpdateFlightStatusDto
    {
        [JsonPropertyName("newStatus")]
        public string NewStatus { get; set; } = string.Empty;
    }
}