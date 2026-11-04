using Catalog.Application.Abstractions;
using Catalog.Application.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Application.Products.Commands;

public sealed record UpdateProductCommand(
    Guid Id, string Name, string Description, decimal Price, string Category);

public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(2000);
        RuleFor(command => command.Category).NotEmpty().MaximumLength(100);

        RuleFor(command => command.Price)
            .GreaterThan(0)
            .Must(price => decimal.Round(price, 2) == price)
            .WithMessage("Price must have at most 2 decimal places.");
    }
}

public sealed class UpdateProductHandler(
    ICatalogDbContext db,
    IValidator<UpdateProductCommand> validator)
    : ICommandHandler<UpdateProductCommand>
{
    public async Task<Result> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        if (await validator.CheckAsync(command, cancellationToken) is { } error)
        {
            return Result.Fail(error);
        }

        // Tracked on purpose: this is a write, and the entity enforces its own invariants
        // through Update() rather than being mutated property by property from here.
        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);

        if (product is null)
        {
            return Result.Fail(Error.NotFound("Product.NotFound", $"No product with id {command.Id}."));
        }

        product.Update(command.Name, command.Description, command.Price, command.Category);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
