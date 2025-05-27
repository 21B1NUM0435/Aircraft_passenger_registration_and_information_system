using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlightManagementSystem.Core.Models
{
    public enum SeatClass
    {
        Economy,
        Business,
        FirstClass
    }

    public class Seat
    {
        public string SeatId { get; set; } = string.Empty;
        public string AircraftId { get; set; } = string.Empty;
        public string SeatNumber { get; set; } = string.Empty;
        public SeatClass SeatClass { get; set; }
        public decimal Price { get; set; }

        // Navigation properties
        public Aircraft Aircraft { get; set; } = null!;
        public ICollection<SeatAssignment> SeatAssignments { get; set; } = new List<SeatAssignment>();
    }
}
