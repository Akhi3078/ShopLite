namespace Catalog.Api.Domain;

/// <summary>
/// A sellable product. Catalog owns description and price only.
/// </summary>
/// <remarks>
/// There is deliberately no stock quantity here. Availability belongs to Inventory:
/// it changes for different reasons, at a different rate, under different load, and
/// putting it on Product is how a catalog service and an inventory service quietly
/// become one service. See ARCHITECTURE.md §5.
/// </remarks>
public sealed class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public decimal Price { get; private set; }
    public string Category { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    // EF Core materialises entities through this; application code uses Create.
    private Product()
    {
    }

    public static Product Create(string name, string description, decimal price, string category) => new()
    {
        // Version 7 GUIDs are time-ordered, so they append to the clustered index
        // instead of scattering inserts across it the way Guid.NewGuid() does.
        Id = Guid.CreateVersion7(),
        Name = name.Trim(),
        Description = description.Trim(),
        Price = price,
        Category = category.Trim(),
        CreatedAt = DateTimeOffset.UtcNow
    };

    public void Update(string name, string description, decimal price, string category)
    {
        Name = name.Trim();
        Description = description.Trim();
        Price = price;
        Category = category.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
