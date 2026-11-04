namespace Catalog.Api.Features.Products;

/// <summary>
/// Shared field rules for create and update. Deliberately plain: swapping this for
/// FluentValidation later is a contained change because nothing else depends on how
/// the errors are produced, only on their shape.
/// </summary>
internal static class ProductValidation
{
    public static Dictionary<string, string[]> Validate(
        string? name, string? description, decimal price, string? category)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors[nameof(name)] = ["Name is required."];
        }
        else if (name.Length > 200)
        {
            errors[nameof(name)] = ["Name must be 200 characters or fewer."];
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            errors[nameof(description)] = ["Description is required."];
        }
        else if (description.Length > 2000)
        {
            errors[nameof(description)] = ["Description must be 2000 characters or fewer."];
        }

        if (price <= 0)
        {
            errors[nameof(price)] = ["Price must be greater than zero."];
        }
        else if (decimal.Round(price, 2) != price)
        {
            errors[nameof(price)] = ["Price must have at most 2 decimal places."];
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            errors[nameof(category)] = ["Category is required."];
        }
        else if (category.Length > 100)
        {
            errors[nameof(category)] = ["Category must be 100 characters or fewer."];
        }

        return errors;
    }
}
