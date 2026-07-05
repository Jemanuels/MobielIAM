using System.Threading.Tasks;
using MauiApp1.Models;

namespace MauiApp1.Services {
    public interface IFirebaseAuthService {
        Task<AuthResult> RegisterAsync(string email, string password, string displayName);
        Task<AuthResult> LoginAsync(string email, string password);
        Task SignOutAsync();
        Task<string?> GetIdTokenAsync();
        Task<string?> GetUidAsync();
        Task<bool> IsSignedInAsync();

        // Reads the stored token + uid from SecureStorage and re-fetches the
        // user profile from Firestore so CurrentUserService is populated.
        // Returns true on success. On failure, the stored credentials are
        // cleared and the caller should show the login screen.
        Task<bool> RestoreSessionAsync();
    }
}
