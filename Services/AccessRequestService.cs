using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MauiApp1.Helpers;
using MauiApp1.Models;

namespace MauiApp1.Services {
    public class AccessRequestService : IAccessRequestService {
        private readonly HttpClient _http;
        private readonly IFirebaseAuthService _auth;

        public AccessRequestService(HttpClient http, IFirebaseAuthService auth) {
            _http = http;
            _auth = auth;
        }

        // ---------- create ----------

        public async Task<string> CreateAsync(AccessRequest request) {
            var token = await GetTokenOrThrowAsync();

            var id = Guid.NewGuid().ToString("N");
            var url = $"{Constants.FirestoreBaseUrl}/accessRequests?documentId={id}";

            var nowIso = DateTime.UtcNow.ToString("o");
            var fields = new Dictionary<string, object> {
                ["requesterUid"] = new { stringValue = request.RequesterUid },
                ["requesterDisplayName"] = new { stringValue = request.RequesterDisplayName },
                ["requesterEmail"] = new { stringValue = request.RequesterEmail },
                ["businessSystemId"] = new { stringValue = request.BusinessSystemId },
                ["businessSystemName"] = new { stringValue = request.BusinessSystemName },
                ["accessRoleRequested"] = new { stringValue = request.AccessRoleRequested },
                ["justification"] = new { stringValue = request.Justification },
                ["status"] = new { stringValue = request.Status.ToString() },
                ["createdAt"] = new { timestampValue = nowIso },
                ["updatedAt"] = new { timestampValue = nowIso }
            };

            var doc = new { fields };
            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent(doc) };
            req.Headers.Authorization = Bearer(token);

            var resp = await _http.SendAsync(req).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) {
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"Firestore create failed ({(int)resp.StatusCode}): {body}");
            }
            return id;
        }

        // ---------- reads ----------

        public Task<IList<AccessRequest>> GetMineAsync(string requesterUid) =>
            RunQueryAsync(new {
                structuredQuery = new {
                    from = new[] { new { collectionId = "accessRequests" } },
                    where = new {
                        fieldFilter = new {
                            field = new { fieldPath = "requesterUid" },
                            op = "EQUAL",
                            value = new { stringValue = requesterUid }
                        }
                    },
                    orderBy = new[] {
                        new { field = new { fieldPath = "createdAt" }, direction = "DESCENDING" }
                    }
                }
            });

        public Task<IList<AccessRequest>> GetPendingAsync() =>
            RunQueryAsync(new {
                structuredQuery = new {
                    from = new[] { new { collectionId = "accessRequests" } },
                    where = new {
                        fieldFilter = new {
                            field = new { fieldPath = "status" },
                            op = "IN",
                            value = new {
                                arrayValue = new {
                                    values = new object[] {
                                        new { stringValue = "Submitted" },
                                        new { stringValue = "PendingApproval" }
                                    }
                                }
                            }
                        }
                    },
                    orderBy = new[] {
                        new { field = new { fieldPath = "createdAt" }, direction = "DESCENDING" }
                    }
                }
            });

        public async Task<AccessRequest?> GetByIdAsync(string requestId) {
            var token = await GetTokenOrThrowAsync();

            var url = $"{Constants.FirestoreBaseUrl}/accessRequests/{requestId}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = Bearer(token);

            var resp = await _http.SendAsync(req).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;

            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) {
                throw new HttpRequestException($"Firestore get failed ({(int)resp.StatusCode}): {body}");
            }
            return ParseSingleDocument(body);
        }

        // ---------- writes ----------

        public Task CancelAsync(string requestId) =>
            PatchAsync(requestId,
                new[] { "status", "updatedAt" },
                new Dictionary<string, object> {
                    ["status"] = new { stringValue = RequestStatus.Cancelled.ToString() },
                    ["updatedAt"] = new { timestampValue = DateTime.UtcNow.ToString("o") }
                });

        public Task ApproveAsync(string requestId, string approverUid, string approverDisplayName, string? comments) =>
            DecideAsync(requestId, RequestStatus.Approved, approverUid, approverDisplayName, comments);

        public Task RejectAsync(string requestId, string approverUid, string approverDisplayName, string? comments) =>
            DecideAsync(requestId, RequestStatus.Rejected, approverUid, approverDisplayName, comments);

        private Task DecideAsync(string requestId, RequestStatus newStatus,
                                 string approverUid, string approverDisplayName, string? comments) {
            var nowIso = DateTime.UtcNow.ToString("o");
            return PatchAsync(requestId,
                new[] { "status", "approverUid", "approverDisplayName", "approvalComments", "decidedAt", "updatedAt" },
                new Dictionary<string, object> {
                    ["status"] = new { stringValue = newStatus.ToString() },
                    ["approverUid"] = new { stringValue = approverUid },
                    ["approverDisplayName"] = new { stringValue = approverDisplayName },
                    ["approvalComments"] = new { stringValue = comments ?? string.Empty },
                    ["decidedAt"] = new { timestampValue = nowIso },
                    ["updatedAt"] = new { timestampValue = nowIso }
                });
        }

        // ---------- shared HTTP helpers ----------

        private async Task<string> GetTokenOrThrowAsync() {
            var token = await _auth.GetIdTokenAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token)) {
                throw new InvalidOperationException("Not signed in.");
            }
            return token!;
        }

        private static AuthenticationHeaderValue Bearer(string token) =>
            new AuthenticationHeaderValue("Bearer", token);

        private static StringContent JsonContent(object payload) =>
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        private async Task<IList<AccessRequest>> RunQueryAsync(object query) {
            var token = await GetTokenOrThrowAsync();
            var url = $"{Constants.FirestoreBaseUrl}:runQuery";

            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent(query) };
            req.Headers.Authorization = Bearer(token);

            var resp = await _http.SendAsync(req).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) {
                throw new HttpRequestException($"Firestore query failed ({(int)resp.StatusCode}): {body}");
            }
            return ParseQueryResponse(body);
        }

        private async Task PatchAsync(string requestId, string[] fieldPaths, Dictionary<string, object> fields) {
            var token = await GetTokenOrThrowAsync();

            var mask = string.Join("&", fieldPaths.Select(f => $"updateMask.fieldPaths={Uri.EscapeDataString(f)}"));
            var url = $"{Constants.FirestoreBaseUrl}/accessRequests/{requestId}?{mask}";

            var doc = new { fields };
            using var req = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = JsonContent(doc) };
            req.Headers.Authorization = Bearer(token);

            var resp = await _http.SendAsync(req).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) {
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"Firestore patch failed ({(int)resp.StatusCode}): {body}");
            }
        }

        // ---------- parsing ----------

        private static IList<AccessRequest> ParseQueryResponse(string body) {
            var results = new List<AccessRequest>();
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return results;

            foreach (var item in doc.RootElement.EnumerateArray()) {
                if (!item.TryGetProperty("document", out var documentEl)) continue;
                var req = BuildFromDocumentElement(documentEl);
                if (req is not null) results.Add(req);
            }
            return results;
        }

        private static AccessRequest? ParseSingleDocument(string body) {
            using var doc = JsonDocument.Parse(body);
            return BuildFromDocumentElement(doc.RootElement);
        }

        private static AccessRequest? BuildFromDocumentElement(JsonElement documentEl) {
            if (!documentEl.TryGetProperty("fields", out var fields)) return null;

            var name = documentEl.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            var id = name?.Split('/').LastOrDefault() ?? string.Empty;

            return new AccessRequest {
                Id = id,
                RequesterUid = TryGetString(fields, "requesterUid") ?? string.Empty,
                RequesterDisplayName = TryGetString(fields, "requesterDisplayName") ?? string.Empty,
                RequesterEmail = TryGetString(fields, "requesterEmail") ?? string.Empty,
                BusinessSystemId = TryGetString(fields, "businessSystemId") ?? string.Empty,
                BusinessSystemName = TryGetString(fields, "businessSystemName") ?? string.Empty,
                AccessRoleRequested = TryGetString(fields, "accessRoleRequested") ?? string.Empty,
                Justification = TryGetString(fields, "justification") ?? string.Empty,
                Status = ParseStatus(TryGetString(fields, "status")),
                CreatedAt = TryGetTimestamp(fields, "createdAt") ?? DateTime.MinValue,
                UpdatedAt = TryGetTimestamp(fields, "updatedAt") ?? DateTime.MinValue,
                ApproverUid = TryGetString(fields, "approverUid"),
                ApproverDisplayName = TryGetString(fields, "approverDisplayName"),
                ApprovalComments = TryGetString(fields, "approvalComments"),
                DecidedAt = TryGetTimestamp(fields, "decidedAt"),
                ProvisioningStatus = TryGetString(fields, "provisioningStatus"),
                ProvisioningMessage = TryGetString(fields, "provisioningMessage"),
                ProvisionedAt = TryGetTimestamp(fields, "provisionedAt")
            };
        }

        private static string? TryGetString(JsonElement fields, string name) {
            if (fields.TryGetProperty(name, out var field) &&
                field.TryGetProperty("stringValue", out var sv)) {
                return sv.GetString();
            }
            return null;
        }

        private static DateTime? TryGetTimestamp(JsonElement fields, string name) {
            if (fields.TryGetProperty(name, out var field) &&
                field.TryGetProperty("timestampValue", out var tv) &&
                tv.GetString() is string s &&
                DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)) {
                return dt;
            }
            return null;
        }

        private static RequestStatus ParseStatus(string? raw) {
            if (Enum.TryParse<RequestStatus>(raw, ignoreCase: true, out var s)) return s;
            return RequestStatus.Draft;
        }
    }
}
