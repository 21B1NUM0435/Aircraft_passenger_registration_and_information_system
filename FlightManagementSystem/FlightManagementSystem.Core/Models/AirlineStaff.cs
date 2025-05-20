using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlightManagementSystem.Core.Models
{
    public class AirlineStaff
    {
        public string StaffId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        // Navigation properties
        public ICollection<CheckInCounter> CheckInCounters { get; set; } = new List<CheckInCounter>();
        public ICollection<CheckInRecord> CheckInRecords { get; set; } = new List<CheckInRecord>();
    }
}
