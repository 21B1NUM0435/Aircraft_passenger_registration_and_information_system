using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlightManagementSystem.Core.Models
{
    public class Aircraft
    {
        public string AircraftId { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public DateTime ManufacturedDate { get; set; }

        // Navigation properties
        public ICollection<Flight> Flights { get; set; } = new List<Flight>();
        public ICollection<Seat> Seats { get; set; } = new List<Seat>();
    }
}
