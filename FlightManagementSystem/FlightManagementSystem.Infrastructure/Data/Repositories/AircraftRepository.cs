using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightManagementSystem.Core.Interfaces;
using FlightManagementSystem.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FlightManagementSystem.Infrastructure.Data.Repositories
{
    public class AircraftRepository : GenericRepository<Aircraft>, IAircraftRepository
    {
        public AircraftRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Aircraft?> GetWithDetailsAsync(string aircraftId)
        {
            return await _context.Aircraft
                .Include(a => a.Flights)
                .Include(a => a.Seats)
                .FirstOrDefaultAsync(a => a.AircraftId == aircraftId);
        }
    }
}
