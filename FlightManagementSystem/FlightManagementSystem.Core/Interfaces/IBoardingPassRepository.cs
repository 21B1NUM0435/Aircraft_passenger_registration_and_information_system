using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightManagementSystem.Core.Models;

namespace FlightManagementSystem.Core.Interfaces
{
    public interface IBoardingPassRepository : IRepository<BoardingPass>
    {
        Task<BoardingPass?> GetByBookingReferenceAsync(string bookingReference);
        Task<bool> HasBoardingPassAsync(string bookingReference);
    }
}
