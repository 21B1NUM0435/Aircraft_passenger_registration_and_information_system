using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlightManagementSystem.Core.Interfaces
{
    public interface ISocketServer
    {
        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync();
        void BroadcastMessage(string message);
    }
}
