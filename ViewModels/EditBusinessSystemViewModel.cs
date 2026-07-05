using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Services;

namespace MauiApp1.ViewModels {
    public partial class EditBusinessSystemViewModel : ObservableObject, IQueryAttributable {
        private readonly IBusinessSystemService _systems;

        public EditBusinessSystemViewModel(IBusinessSystemService systems) {
            _systems = systems;
        }

        [ObservableProperty] private string _pageTitle = "Add business system";
        [ObservableProperty] private bool _isEditMode;
        [ObservableProperty] private bool _isBusy;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasError))]
        private string? _errorMessage;
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        private string? _editingId;

        // ---- Core fields ----
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string _name = string.Empty;

        [ObservableProperty] private string _description = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string _rolesText = string.Empty;

        [ObservableProperty] private bool _isActive = true;

        // ---- Entra mapping ----
        // ServicePrincipalId: the Object ID of the target Enterprise Application's
        // service principal in Entra. Leave blank to skip provisioning for this system.
        [ObservableProperty] private string _entraServicePrincipalId = string.Empty;

        // Role mappings, entered as text. One line per mapping in the form:
        //   RoleName=GUID
        // e.g.   Standard User=63406193-1ac3-4d89-9b08-476b439ab42b
        [ObservableProperty] private string _entraRoleMappingsText = string.Empty;

        // ---- Lifecycle ----

        public void ApplyQueryAttributes(IDictionary<string, object> query) {
            if (query.TryGetValue("systemId", out var idObj) && idObj is string id && !string.IsNullOrWhiteSpace(id)) {
                _editingId = id;
                IsEditMode = true;
                PageTitle = "Edit business system";
                _ = LoadAsync(id);
            }
            else {
                _editingId = null;
                IsEditMode = false;
                PageTitle = "Add business system";
            }
        }

        private async Task LoadAsync(string id) {
            IsBusy = true;
            ErrorMessage = null;
            try {
                var s = await _systems.GetByIdAsync(id);
                if (s is null) {
                    ErrorMessage = "System not found.";
                    return;
                }
                Name = s.Name;
                Description = s.Description;
                RolesText = string.Join(", ", s.AvailableRoles);
                IsActive = s.IsActive;
                EntraServicePrincipalId = s.EntraServicePrincipalId;
                EntraRoleMappingsText = string.Join(
                    Environment.NewLine,
                    s.EntraAppRoleMappings.Select(kv => $"{kv.Key}={kv.Value}"));
            }
            catch (Exception ex) {
                ErrorMessage = $"Couldn't load: {ex.Message}";
            }
            finally {
                IsBusy = false;
            }
        }

        // ---- Commands ----

        private bool CanSave() =>
            !IsBusy
            && !string.IsNullOrWhiteSpace(Name)
            && !string.IsNullOrWhiteSpace(RolesText);

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync() {
            IsBusy = true;
            ErrorMessage = null;
            try {
                var roles = ParseRoles(RolesText);
                if (roles.Count == 0) {
                    ErrorMessage = "Add at least one role (comma-separated).";
                    return;
                }

                var roleMappings = ParseRoleMappings(EntraRoleMappingsText);

                // Sanity check: if the admin has filled in mappings, all keys should
                // match a role from RolesText. Warn (don't block) if any are off.
                if (roleMappings.Count > 0) {
                    var unknown = roleMappings.Keys
                        .Where(k => !roles.Any(r => string.Equals(r, k, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                    if (unknown.Count > 0) {
                        ErrorMessage = $"These role mapping keys don't match any role in the list above: {string.Join(", ", unknown)}";
                        return;
                    }
                }

                if (IsEditMode && !string.IsNullOrWhiteSpace(_editingId)) {
                    var updated = new BusinessSystem {
                        Id = _editingId!,
                        Name = Name.Trim(),
                        Description = Description?.Trim() ?? string.Empty,
                        AvailableRoles = roles,
                        IsActive = IsActive,
                        EntraServicePrincipalId = EntraServicePrincipalId?.Trim() ?? string.Empty,
                        EntraAppRoleMappings = roleMappings
                    };
                    await _systems.UpdateAsync(updated);
                }
                else {
                    var created = new BusinessSystem {
                        Name = Name.Trim(),
                        Description = Description?.Trim() ?? string.Empty,
                        AvailableRoles = roles,
                        IsActive = IsActive,
                        EntraServicePrincipalId = EntraServicePrincipalId?.Trim() ?? string.Empty,
                        EntraAppRoleMappings = roleMappings
                    };
                    await _systems.CreateAsync(created);
                }

                if (Shell.Current is not null) {
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch (Exception ex) {
                ErrorMessage = $"Couldn't save: {ex.Message}";
            }
            finally {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CancelAsync() {
            if (Shell.Current is not null) {
                await Shell.Current.GoToAsync("..");
            }
        }

        // ---- Parsing ----

        private static List<string> ParseRoles(string raw) {
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                      .Where(r => !string.IsNullOrWhiteSpace(r))
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .ToList();
        }

        // Parses lines of the form "RoleName=GUID" into a dictionary.
        // Whitespace around either side of '=' is trimmed. Lines without '='
        // or with empty values are silently skipped.
        private static Dictionary<string, string> ParseRoleMappings(string raw) {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw)) return result;

            foreach (var line in raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                var trimmed = line.Trim();
                if (trimmed.Length == 0) continue;

                var idx = trimmed.IndexOf('=');
                if (idx <= 0 || idx >= trimmed.Length - 1) continue;

                var key = trimmed.Substring(0, idx).Trim();
                var value = trimmed.Substring(idx + 1).Trim();
                if (key.Length > 0 && value.Length > 0) {
                    result[key] = value;
                }
            }
            return result;
        }
    }
}
