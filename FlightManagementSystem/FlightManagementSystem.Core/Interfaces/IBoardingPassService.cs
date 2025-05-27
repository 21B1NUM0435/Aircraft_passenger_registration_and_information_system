using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlightManagementSystem.Core.Models;

namespace FlightManagementSystem.Core.Interfaces
{
    public interface IBoardingPassService
    {
        Task<BoardingPass?> GetBoardingPassAsync(string bookingReference);
        Task<BoardingPass> GenerateBoardingPassAsync(Booking booking, string gate);
        Task<byte[]> GenerateBoardingPassPdfAsync(BoardingPass boardingPass);
    }
}