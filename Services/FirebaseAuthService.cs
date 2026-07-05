using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MauiApp1.Helpers;
using MauiApp1.Models;

namespace MauiApp1.Services {
    // Minimal Firebase Auth service using REST endpoints (Identity Toolkit).
    // We use REST because the native Firebase SDKs don't ship for .NET MAUI cleanly.
    public class FirebaseAuthService : IFirebaseAuthService {
        private readonly HttpClient _http;
        private readonly ICurrentUserService _currentUser;

        private const string IdTokenKey = "firebase_id_token";
        private const string UidKey = "firebase_uid";

        private static readonly JsonSerializerOptions JsonOpts =
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public FirebaseAuthService(HttpClient http, ICurrentUserService currentUser) {
            _http = http;
            _currentUser = currentUser;
        }

        public async Task<AuthResult> RegisterAsync(string email, string password, string displayName) {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) {
                return AuthResult.Fail("Email and password are required.");
            }

            var signupUrl = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={Constants.FirebaseApiKey}";
            var payload = new { email, password, returnSecureToken = true };

            var resp = await PostJsonAsync(signupUrl, payload).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode) {
                return AuthResult.Fail(ParseFirebaseError(body));
            }

            var authResp = JsonSerializer.Deserialize<FirebaseAuthResponse>(body, JsonOpts);
            if (authResp?.IdToken is null || authResp.LocalId is null) {
                return AuthResult.Fail("Unexpected response from Firebase.");
            }

            // Set displayName via the update endpoint (best-effort).
            if (!string.IsNullOrWhiteSpace(displayName)) {
                var updateUrl = $"https://identitytoolkit.googleapis.com/v1/accounts:update?key={Constants.FirebaseApiKey}";
                var updatePayload = new { idToken = authResp.IdToken, displayName, returnSecureToken = false };
                try { await PostJsonAsync(updateUrl, updatePayload).ConfigureAwait(false); }
                catch { /* non-fatal */ }
            }

            // Write the user doc to Firestore. New users always start as Requester.
            try {
                await CreateFirestoreUserDocAsync(authResp.LocalId, email, displayName, authResp.IdToken).ConfigureAwait(false);
            }
            catch { /* non-fatal */ }

            await SecureStorage.SetAsync(IdTokenKey, authResp.IdToken).ConfigureAwait(false);
            await SecureStorage.SetAsync(UidKey, authResp.LocalId).ConfigureAwait(false);

            // We just wrote the user with role=Requester, so we can build the
            // profile directly without re-fetching from Firestore.
            _currentUser.Set(new AppUser {
                Uid = authResp.LocalId,
                Email = email,
                DisplayName = displayName ?? string.Empty,
                Role = UserRole.Requester
            });

            return AuthResult.Ok(authResp);
        }

        public async Task<AuthResult> LoginAsync(string email, string password) {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) {
                return AuthResult.Fail("Email and password are required.");
            }

            var signInUrl = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={Constants.FirebaseApiKey}";
            var payload = new { email, password, returnSecureToken = true };

            var resp = await PostJsonAsync(signInUrl, payload).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode) {
                return AuthResult.Fail(ParseFirebaseError(body));
            }

            var authResp = JsonSerializer.Deserialize<FirebaseAuthResponse>(body, JsonOpts);
            if (authResp?.IdToken is null || authResp.LocalId is null) {
                return AuthResult.Fail("Unexpected response from Firebase.");
            }

            await SecureStorage.SetAsync(IdTokenKey, authResp.IdToken).ConfigureAwait(false);
            await SecureStorage.SetAsync(UidKey, authResp.LocalId).ConfigureAwait(false);

            // Fetch the user profile (including role) from Firestore.
            var profile = await FetchUserProfileAsync(authResp.LocalId, authResp.IdToken).ConfigureAwait(false);
            if (profile is null) {
                // User exists in Firebase Auth but has no Firestore doc — fall back
                // to a default Requester profile. This handles older accounts that
                // were created before we had user docs.
                profile = new AppUser {
                    Uid = authResp.LocalId,
                    Email = email,
                    DisplayName = authResp.Email ?? email,
                    Role = UserRole.Requester
                };
            }
            _currentUser.Set(profile);

            return AuthResult.Ok(authResp);
        }

        public Task SignOutAsync() {
            SecureStorage.Remove(IdTokenKey);
            SecureStorage.Remove(UidKey);
            _currentUser.Clear();
            return Task.CompletedTask;
        }

        public async Task<string?> GetIdTokenAsync() {
            try { return await SecureStorage.GetAsync(IdTokenKey).ConfigureAwait(false); }
            catch { return null; }
        }

        public async Task<string?> GetUidAsync() {
            try { return await SecureStorage.GetAsync(UidKey).ConfigureAwait(false); }
            catch { return null; }
        }

        public async Task<bool> IsSignedInAsync() {
            var token = await GetIdTokenAsync().ConfigureAwait(false);
            return !string.IsNullOrWhiteSpace(token);
        }

        public async Task<bool> RestoreSessionAsync() {
            var token = await GetIdTokenAsync().ConfigureAwait(false);
            var uid = await GetUidAsync().ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(uid)) {
                return false;
            }

            try {
                var profile = await FetchUserProfileAsync(uid!, token!).ConfigureAwait(false);
                if (profile is null) {
                    // Token may be expired or revoked. Wipe and force re-login.
                    await SignOutAsync().ConfigureAwait(false);
                    return false;
                }
                _currentUser.Set(profile);
                return true;
            }
            catch {
                await SignOutAsync().ConfigureAwait(false);
                return false;
            }
        }

        // -------------------- helpers --------------------

        private Task<HttpResponseMessage> PostJsonAsync(string url, object payload) {
            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");
            return _http.PostAsync(url, content);
        }

        private async Task CreateFirestoreUserDocAsync(string uid, string email, string? displayName, string idToken) {
            var url = $"{Constants.FirestoreBaseUrl}/users?documentId={uid}";
            var doc = new {
                fields = new {
                    email = new { stringValue = email },
                    displayName = new { stringValue = displayName ?? string.Empty },
                    role = new { stringValue = "Requester" },
                    createdAt = new { timestampValue = DateTime.UtcNow.ToString("o") }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url) {
                Content = new StringContent(JsonSerializer.Serialize(doc), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

            await _http.SendAsync(request).ConfigureAwait(false);
        }

        private async Task<AppUser?> FetchUserProfileAsync(string uid, string idToken) {
            var url = $"{Constants.FirestoreBaseUrl}/users/{uid}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

            var resp = await _http.SendAsync(request).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) {
                return null;
            }

            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            try {
                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("fields", out var fields)) {
                    return null;
                }

                return new AppUser {
                    Uid = uid,
                    Email = TryGetStringField(fields, "email") ?? string.Empty,
                    DisplayName = TryGetStringField(fields, "displayName") ?? string.Empty,
                    Role = ParseRole(TryGetStringField(fields, "role"))
                };
            }
            catch {
                return null;
            }
        }

        private static string? TryGetStringField(JsonElement fields, string name) {
            if (fields.TryGetProperty(name, out var field) &&
                field.TryGetProperty("stringValue", out var sv)) {
                return sv.GetString();
            }
            return null;
        }

        private static UserRole ParseRole(string? raw) {
            if (string.IsNullOrWhiteSpace(raw)) return UserRole.Requester;
            return raw.Trim().ToLowerInvariant() switch {
                "admin" => UserRole.Admin,
                "approver" => UserRole.Approver,
                _ => UserRole.Requester
            };
        }

        // Firebase error bodies look like: { "error": { "code": 400, "message": "EMAIL_EXISTS", ... } }
        private static string ParseFirebaseError(string body) {
            try {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var err) &&
                    err.TryGetProperty("message", out var msgEl)) {
                    var raw = msgEl.GetString() ?? "Unknown error";
                    var code = raw.Split(':')[0].Trim();
                    return code switch {
                        "EMAIL_EXISTS" => "An account with that email already exists.",
                        "OPERATION_NOT_ALLOWED" => "Email/password sign-in is not enabled in Firebase. Enable it in the Firebase console.",
                        "TOO_MANY_ATTEMPTS_TRY_LATER" => "Too many attempts. Please try again later.",
                        "EMAIL_NOT_FOUND" => "No account found with that email.",
                        "INVALID_PASSWORD" => "Incorrect password.",
                        "INVALID_LOGIN_CREDENTIALS" => "Invalid email or password.",
                        "USER_DISABLED" => "This account has been disabled.",
                        "WEAK_PASSWORD" => "Password is too weak. Use at least 6 characters.",
                        "INVALID_EMAIL" => "That email address is not valid.",
                        "MISSING_PASSWORD" => "Please enter a password.",
                        "MISSING_EMAIL" => "Please enter an email.",
                        _ => raw
                    };
                }
            }
            catch { /* fall through */ }
            return "Authentication failed. Please try again.";
        }
    }
}
