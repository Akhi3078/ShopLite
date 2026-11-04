using Catalog.Application.Abstractions;
using Catalog.Application.Common;
using Catalog.Domain.Entities;
using FluentValidation;

namespace Catalog.Application.Products.Commands;

public sealed record CreateProductCommand(string Name, string Description, decimal Price, string Category);

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(2000);
        RuleFor(command => command.Category).NotEmpty().MaximumLength(100);

        RuleFor(command => command.Price)
            .GreaterThan(0)
            .Must(price => decimal.Round(price, 2) == price)
            .WithMessage("Price must have at most 2 decimal places.");
    }
}

public sealed class CreateProductHandler(
    ICatalogDbContext db,
    IValidator<CreateProductCommand> validator)
    : ICommandHandler<CreateProductCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        if (await validator.CheckAsync(command, cancellationToken) is { } error)
        {
            return Result<Guid>.Fail(error);
        }

        var product = Product.Create(command.Name, command.Description, command.Price, command.Category);

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(product.Id);
    }
}
