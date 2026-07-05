namespace MauiApp1.Models {
    // The currently signed-in user, as held by the app in memory.
    // Populated from the Firestore /users/{uid} document on login.
    public class AppUser {
        public string Uid { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Requester;
    }
}
