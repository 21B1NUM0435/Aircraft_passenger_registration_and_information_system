using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlightManagementSystem.Core.Models
{
    public enum CheckInStatus
    {
        NotCheckedIn,
        CheckedIn
    }

    public enum PaymentStatus
    {
        Pending,
        Completed,
        Failed,
        Refunded
    }

    public class Booking
    {
        public string BookingReference { get; set; } = string.Empty;
        public string PassengerId { get; set; } = string.Empty;
        public string FlightNumber { get; set; } = string.Empty;
        public DateTime BookingDate { get; set; }
        public CheckInStatus CheckInStatus { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public decimal TotalPrice { get; set; }

        // Navigation properties
        public Passenger Passenger { get; set; } = null!;
        public Flight Flight { get; set; } = null!;
        public ICollection<SeatAssignment> SeatAssignments { get; set; } = new List<SeatAssignment>();
        public ICollection<BoardingPass> BoardingPasses { get; set; } = new List<BoardingPass>();
        public ICollection<CheckInRecord> CheckInRecords { get; set; } = new List<CheckInRecord>();
    }
}
