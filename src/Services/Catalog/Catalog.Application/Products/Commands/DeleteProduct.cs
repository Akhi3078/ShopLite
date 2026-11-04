using Catalog.Application.Abstractions;
using Catalog.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Application.Products.Commands;

public sealed record DeleteProductCommand(Guid Id);

public sealed class DeleteProductHandler(ICatalogDbContext db)
    : ICommandHandler<DeleteProductCommand>
{
    public async Task<Result> Handle(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        // ExecuteDeleteAsync issues a single DELETE rather than loading the entity first.
        // Safe here because deletion carries no domain behaviour; it would be the wrong
        // choice if the entity needed to raise an event or check an invariant on removal.
        var deleted = await db.Products
            .Where(product => product.Id == command.Id)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0
            ? Result.Fail(Error.NotFound("Product.NotFound", $"No product with id {command.Id}."))
            : Result.Success();
    }
}
