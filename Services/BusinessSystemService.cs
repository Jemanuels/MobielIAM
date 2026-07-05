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
    public class BusinessSystemService : IBusinessSystemService {
        private readonly HttpClient _http;
        private readonly IFirebaseAuthService _auth;

        public BusinessSystemService(HttpClient http, IFirebaseAuthService auth) {
            _http = http;
            _auth = auth;
        }

        // ---------- reads ----------

        public async Task<IList<BusinessSystem>> GetAllAsync() {
            var token = await GetTokenOrThrowAsync();
            var url = $"{Constants.FirestoreBaseUrl}/businessSystems";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = Bearer(token);

            var resp = await _http.SendAsync(req).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) {
                throw new HttpRequestException($"Firestore list failed ({(int)resp.StatusCode}): {body}");
            }

            return ParseListResponse(body)
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<IList<BusinessSystem>> GetActiveAsync() {
            var all = await GetAllAsync();
            return all.Where(s => s.IsActive).ToList();
        }

        public async Task<BusinessSystem?> GetByIdAsync(string id) {
            var token = await GetTokenOrThrowAsync();
            var url = $"{Constants.FirestoreBaseUrl}/businessSystems/{id}";

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

        public async Task<string> CreateAsync(BusinessSystem system) {
            var token = await GetTokenOrThrowAsync();

            var id = string.IsNullOrWhiteSpace(system.Id)
                ? Guid.NewGuid().ToString("N")
                : system.Id;

            var url = $"{Constants.FirestoreBaseUrl}/businessSystems?documentId={id}";
            var nowIso = DateTime.UtcNow.ToString("o");

            var doc = new { fields = BuildFieldsObject(system, nowIso, nowIso) };

            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent(doc) };
            req.Headers.Authorization = Bearer(token);

            var resp = await _http.SendAsync(req).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) {
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"Firestore create failed ({(int)resp.StatusCode}): {body}");
            }
            return id;
        }

        public async Task UpdateAsync(BusinessSystem system) {
            if (string.IsNullOrWhiteSpace(system.Id)) {
                throw new ArgumentException("System ID required for update.", nameof(system));
            }

            var token = await GetTokenOrThrowAsync();

            var fieldsToUpdate = new[] {
                "name", "description", "isActive", "availableRoles",
                "entraServicePrincipalId", "entraAppRoleMappings", "updatedAt"
            };
            var mask = string.Join("&", fieldsToUpdate.Select(f => $"updateMask.fieldPaths={Uri.EscapeDataString(f)}"));
            var url = $"{Constants.FirestoreBaseUrl}/businessSystems/{system.Id}?{mask}";

            var nowIso = DateTime.UtcNow.ToString("o");
            var fields = new Dictionary<string, object> {
                ["name"] = new { stringValue = system.Name },
                ["description"] = new { stringValue = system.Description },
                ["isActive"] = new { booleanValue = system.IsActive },
                ["availableRoles"] = BuildArrayField(system.AvailableRoles),
                ["entraServicePrincipalId"] = new { stringValue = system.EntraServicePrincipalId ?? string.Empty },
                ["entraAppRoleMappings"] = BuildMapField(system.EntraAppRoleMappings),
                ["updatedAt"] = new { timestampValue = nowIso }
            };

            using var req = new HttpRequestMessage(new HttpMethod("PATCH"), url) {
                Content = JsonContent(new { fields })
            };
            req.Headers.Authorization = Bearer(token);

            var resp = await _http.SendAsync(req).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) {
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"Firestore update failed ({(int)resp.StatusCode}): {body}");
            }
        }

        public async Task SeedExamplesAsync() {
            var examples = new[] {
                new BusinessSystem {
                    Name = "Salesforce Developer",
                    Description = "Salesforce CRM (dev tenant). Provisioned via Microsoft Entra.",
                    AvailableRoles = new List<string> { "Standard User", "Read only" },
                    IsActive = true
                    // EntraServicePrincipalId + role mappings configured by admin in Edit page.
                },
                new BusinessSystem {
                    Name = "Finance System",
                    Description = "General ledger, AP/AR, and financial reporting.",
                    AvailableRoles = new List<string> { "Read-only", "Contributor", "Admin" },
                    IsActive = true
                },
                new BusinessSystem {
                    Name = "SharePoint Sales Site",
                    Description = "Internal documents, proposals, and contracts for the sales team.",
                    AvailableRoles = new List<string> { "Read-only", "Edit", "Owner" },
                    IsActive = true
                }
            };

            foreach (var s in examples) {
                await CreateAsync(s);
            }
        }

        // ---------- helpers ----------

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

        private static object BuildFieldsObject(BusinessSystem s, string createdAtIso, string updatedAtIso) {
            return new Dictionary<string, object> {
                ["name"] = new { stringValue = s.Name },
                ["description"] = new { stringValue = s.Description ?? string.Empty },
                ["availableRoles"] = BuildArrayField(s.AvailableRoles),
                ["isActive"] = new { booleanValue = s.IsActive },
                ["entraServicePrincipalId"] = new { stringValue = s.EntraServicePrincipalId ?? string.Empty },
                ["entraAppRoleMappings"] = BuildMapField(s.EntraAppRoleMappings),
                ["createdAt"] = new { timestampValue = createdAtIso },
                ["updatedAt"] = new { timestampValue = updatedAtIso }
            };
        }

        private static object BuildArrayField(IList<string>? roles) {
            var values = (roles ?? new List<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => (object)new { stringValue = r.Trim() })
                .ToArray();
            return new { arrayValue = new { values } };
        }

        // Firestore map encoding:
        // {"mapValue":{"fields":{"key1":{"stringValue":"v1"}, "key2":{"stringValue":"v2"}}}}
        private static object BuildMapField(IDictionary<string, string>? mappings) {
            var dict = new Dictionary<string, object>();
            if (mappings is not null) {
                foreach (var kv in mappings) {
                    if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                    dict[kv.Key.Trim()] = new { stringValue = kv.Value ?? string.Empty };
                }
            }
            return new { mapValue = new { fields = dict } };
        }

        // ---------- parsing ----------

        private static IList<BusinessSystem> ParseListResponse(string body) {
            var results = new List<BusinessSystem>();
            using var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("documents", out var docs)) return results;
            if (docs.ValueKind != JsonValueKind.Array) return results;

            foreach (var item in docs.EnumerateArray()) {
                var s = BuildFromDocumentElement(item);
                if (s is not null) results.Add(s);
            }
            return results;
        }

        private static BusinessSystem? ParseSingleDocument(string body) {
            using var doc = JsonDocument.Parse(body);
            return BuildFromDocumentElement(doc.RootElement);
        }

        private static BusinessSystem? BuildFromDocumentElement(JsonElement documentEl) {
            if (!documentEl.TryGetProperty("fields", out var fields)) return null;

            var name = documentEl.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            var id = name?.Split('/').LastOrDefault() ?? string.Empty;

            return new BusinessSystem {
                Id = id,
                Name = TryGetString(fields, "name") ?? string.Empty,
                Description = TryGetString(fields, "description") ?? string.Empty,
                AvailableRoles = TryGetStringArray(fields, "availableRoles"),
                IsActive = TryGetBool(fields, "isActive") ?? true,
                EntraServicePrincipalId = TryGetString(fields, "entraServicePrincipalId") ?? string.Empty,
                EntraAppRoleMappings = TryGetStringMap(fields, "entraAppRoleMappings"),
                CreatedAt = TryGetTimestamp(fields, "createdAt") ?? DateTime.MinValue,
                UpdatedAt = TryGetTimestamp(fields, "updatedAt") ?? DateTime.MinValue
            };
        }

        private static string? TryGetString(JsonElement fields, string name) {
            if (fields.TryGetProperty(name, out var field) &&
                field.TryGetProperty("stringValue", out var sv)) {
                return sv.GetString();
            }
            return null;
        }

        private static bool? TryGetBool(JsonElement fields, string name) {
            if (fields.TryGetProperty(name, out var field) &&
                field.TryGetProperty("booleanValue", out var bv)) {
                return bv.GetBoolean();
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

        private static List<string> TryGetStringArray(JsonElement fields, string name) {
            var result = new List<string>();
            if (fields.TryGetProperty(name, out var field) &&
                field.TryGetProperty("arrayValue", out var av) &&
                av.TryGetProperty("values", out var values) &&
                values.ValueKind == JsonValueKind.Array) {
                foreach (var v in values.EnumerateArray()) {
                    if (v.TryGetProperty("stringValue", out var sv)) {
                        var s = sv.GetString();
                        if (!string.IsNullOrEmpty(s)) result.Add(s);
                    }
                }
            }
            return result;
        }

        private static Dictionary<string, string> TryGetStringMap(JsonElement fields, string name) {
            var result = new Dictionary<string, string>();
            if (fields.TryGetProperty(name, out var field) &&
                field.TryGetProperty("mapValue", out var mv) &&
                mv.TryGetProperty("fields", out var mapFields) &&
                mapFields.ValueKind == JsonValueKind.Object) {
                foreach (var entry in mapFields.EnumerateObject()) {
                    if (entry.Value.TryGetProperty("stringValue", out var sv)) {
                        var s = sv.GetString();
                        if (s is not null) result[entry.Name] = s;
                    }
                }
            }
            return result;
        }
    }
}
