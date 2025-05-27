using System;
using System.Threading.Tasks;

namespace FlightManagementSystem.Core.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        Task<int> SaveChangesAsync();
        Task<IUnitOfWorkTransaction> BeginTransactionAsync();
    }

    public interface IUnitOfWorkTransaction : IDisposable
    {
        Task CommitAsync();
        Task RollbackAsync();
    }
}