using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlightManagementSystem.Core.Models
{
    public enum CounterStatus
    {
        Open,
        Closed,
        Maintenance
    }

    public class CheckInCounter
    {
        public string CounterId { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
        public string Terminal { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public CounterStatus Status { get; set; }

        // Navigation properties
        public AirlineStaff Staff { get; set; } = null!;
        public ICollection<CheckInRecord> CheckInRecords { get; set; } = new List<CheckInRecord>();
    }
}
