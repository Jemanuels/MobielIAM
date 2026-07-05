using System.Collections.Generic;
using System.Threading.Tasks;
using MauiApp1.Models;

namespace MauiApp1.Services {
    public interface IBusinessSystemService {
        // Returns every system, including deactivated ones. Used by Admin pages.
        Task<IList<BusinessSystem>> GetAllAsync();

        // Returns only IsActive == true, sorted by name. Used by the requester form.
        Task<IList<BusinessSystem>> GetActiveAsync();

        Task<BusinessSystem?> GetByIdAsync(string id);

        // Creates a doc. Returns the generated ID.
        Task<string> CreateAsync(BusinessSystem system);

        // Updates name, description, isActive, availableRoles. Won't touch
        // createdAt — only updatedAt is overwritten.
        Task UpdateAsync(BusinessSystem system);

        // Convenience for the Admin page's "Seed examples" button.
        Task SeedExamplesAsync();
    }
}
