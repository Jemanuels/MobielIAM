using System;
using MauiApp1.Models;

namespace MauiApp1.Services {
    public interface ICurrentUserService {
        AppUser? Current { get; }
        bool IsSignedIn { get; }
        void Set(AppUser user);
        void Clear();

        // Raised when Current changes. ViewModels can subscribe if they need
        // to react to login/logout while alive.
        event EventHandler? CurrentUserChanged;
    }
}
