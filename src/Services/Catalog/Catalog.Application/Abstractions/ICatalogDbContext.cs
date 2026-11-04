using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Application.Abstractions;

/// <summary>
/// The Application layer's view of persistence. Infrastructure implements it, so the
/// dependency points inward: use cases depend on this interface, never on CatalogDbContext.
/// </summary>
public interface ICatalogDbContext
{
    DbSet<Product> Products { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
