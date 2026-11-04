using Catalog.Application.Common;

namespace Catalog.Application.Abstractions;

/// <summary>
/// CQRS handler contracts, hand-written rather than taken from MediatR, which moved to
/// commercial licensing. Three small interfaces replace the dependency; Wolverine's
/// in-process mediator can take over the dispatch in Phase 3 if the indirection earns it.
/// </summary>
/// <remarks>
/// Commands change state; queries do not. Keeping them as separate interfaces makes the
/// distinction a compile-time fact rather than a naming convention.
/// </remarks>
public interface ICommandHandler<in TCommand>
{
    Task<Result> Handle(TCommand command, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand, TResult>
{
    Task<Result<TResult>> Handle(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResult>
{
    Task<Result<TResult>> Handle(TQuery query, CancellationToken cancellationToken);
}
