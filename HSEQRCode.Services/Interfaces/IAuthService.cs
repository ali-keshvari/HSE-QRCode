using System.Threading.Tasks;
using HSEQRCode.Domain.Entities;

namespace HSEQRCode.Services.Interfaces
{
    /// <summary>
    /// Handles user authentication, password hashing, and per-DataSource RBAC authorization.
    /// </summary>
    public interface IAuthService
    {
        Task<AppUser> ValidateUserAsync(string username, string password);

        string HashPassword(string password);

        Task<bool> IsUserAuthorizedForDataSourceAsync(int dataSourceId, string username);

        Task<AppUser> GetActiveUserAsync(string username);
    }
}
