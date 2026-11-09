using Basket.Application.Common;

namespace Basket.Application.Abstractions;

/// <summary>
/// The same three hand-written CQRS contracts Catalog uses. See ADR 0004 for why these
/// are not MediatR interfaces.
/// </summary>
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
