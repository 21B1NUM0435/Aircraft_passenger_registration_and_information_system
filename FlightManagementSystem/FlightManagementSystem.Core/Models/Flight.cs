using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlightManagementSystem.Core.Models
{
    public enum FlightStatus
    {
        CheckingIn,       // Бүртгэж байна
        Boarding,         // Онгоцонд сууж байна
        Departed,         // Ниссэн
        Delayed,          // Хойшилсон
        Cancelled         // Цуцалсан
    }

    public class Flight
    {
        public string FlightNumber { get; set; } = string.Empty;
        public string AircraftId { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public string Origin { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public FlightStatus Status { get; set; }

        // Navigation properties
        public Aircraft Aircraft { get; set; } = null!;
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}