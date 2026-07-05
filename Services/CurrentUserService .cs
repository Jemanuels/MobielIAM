using System;
using MauiApp1.Models;

namespace MauiApp1.Services {
    public class CurrentUserService : ICurrentUserService {
        public AppUser? Current { get; private set; }
        public bool IsSignedIn => Current is not null;

        public event EventHandler? CurrentUserChanged;

        public void Set(AppUser user) {
            Current = user;
            CurrentUserChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear() {
            Current = null;
            CurrentUserChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
