namespace MauiApp1.Models {
    // Simple result wrapper so the auth service can tell the ViewModel
    // both whether the call succeeded and (if not) a user-friendly error.
    public class AuthResult {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public FirebaseAuthResponse? Data { get; set; }

        public static AuthResult Ok(FirebaseAuthResponse data) =>
            new AuthResult { Success = true, Data = data };

        public static AuthResult Fail(string message) =>
            new AuthResult { Success = false, ErrorMessage = message };
    }
}
