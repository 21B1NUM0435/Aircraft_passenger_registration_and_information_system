using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlightManagementSystem.Core.Models
{
    public class BoardingPass
    {
        public string BoardingPassId { get; set; } = string.Empty;
        public string BookingReference { get; set; } = string.Empty;
        public string Gate { get; set; } = string.Empty;
        public DateTime BoardingTime { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }

        // Navigation properties
        public Booking Booking { get; set; } = null!;
    }
}