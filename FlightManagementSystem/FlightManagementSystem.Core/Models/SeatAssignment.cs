using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlightManagementSystem.Core.Models
{
    public class SeatAssignment
    {
        public string AssignmentId { get; set; } = string.Empty;
        public string BookingReference { get; set; } = string.Empty;
        public string SeatId { get; set; } = string.Empty;
        public DateTime AssignedAt { get; set; }

        // Navigation properties
        public Booking Booking { get; set; } = null!;
        public Seat Seat { get; set; } = null!;
    }
}
