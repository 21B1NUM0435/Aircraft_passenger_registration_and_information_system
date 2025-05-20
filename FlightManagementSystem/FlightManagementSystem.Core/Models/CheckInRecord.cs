using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlightManagementSystem.Core.Models
{
    public enum CheckInMethod
    {
        Counter,
        Online,
        Kiosk
    }

    public class CheckInRecord
    {
        public string CheckInId { get; set; } = string.Empty;
        public string BookingReference { get; set; } = string.Empty;
        public string CounterId { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
        public DateTime CheckInTime { get; set; }
        public CheckInMethod CheckInMethod { get; set; }

        // Navigation properties
        public Booking Booking { get; set; } = null!;
        public CheckInCounter Counter { get; set; } = null!;
        public AirlineStaff Staff { get; set; } = null!;
    }
}
