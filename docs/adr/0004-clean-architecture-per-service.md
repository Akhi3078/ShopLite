# 4. Clean Architecture, four projects per service

**Date:** 2026-11-04
**Status:** Accepted
**Supersedes:** the "vertical slices, not layers" decision in [ADR 0003](0003-catalog-service-shape.md). Every other decision in 0003 still stands.

## Context

ADR 0003 chose vertical slices in a single project per service, on the grounds that layering spreads one change across four folders and that Catalog is CRUD.

The project owner asked for Clean Architecture with a separate project per layer. Both structures are defensible; the deciding factors here are that the pattern is the dominant convention in .NET enterprise work, that it is what reviewers and interviewers expect to see, and that a consistent template across six services is worth more than per-service optimisation.

## Decision

Each service is four projects:

```
Catalog.Domain          entities, invariants. ZERO references.
    ▲
Catalog.Application     use cases, ports, Result types. References Domain.
    ▲
Catalog.Infrastructure  EF Core, DbContext, migrations. References Application.
    ▲
Catalog.Api             HTTP, OpenAPI, composition root. References Application + Infrastructure.
```

The dependency direction is the architecture, and it is enforced by the compiler rather than by discipline: Domain cannot reference EF Core because it has no package references at all, and Api cannot reach the DbContext because it has no EF Core reference.

### CQRS without MediatR

`ICommandHandler<TCommand>`, `ICommandHandler<TCommand, TResult>` and `IQueryHandler<TQuery, TResult>` are hand-written in `Catalog.Application.Abstractions`. MediatR, the conventional choice here, is now commercially licensed. Three interfaces and explicit DI registration replace it. Wolverine's in-process mediator can take over dispatch in Phase 3 if the indirection proves useful.

Handlers are registered explicitly rather than by assembly scanning: verbose, but every dispatch traces to a line of code.

### `ICatalogDbContext`, not repositories

The Application layer depends on an interface exposing `DbSet<Product>`, implemented by `CatalogDbContext`. This is the mainstream .NET Clean Architecture approach and it does mean Application references EF Core.

The purist alternative — repository interfaces returning materialised collections — removes that reference at the cost of hand-writing a query abstraction, and tends to produce either a leaky `IQueryable`-returning interface or a repository per query shape. Rejected as more ceremony than this project will repay. Revisit if Application ever needs to run without EF Core.

### Results, not exceptions, for expected failures

Use cases return `Result` / `Result<T>` carrying an `Error` with an `ErrorType` of `Validation`, `NotFound` or `Conflict`. The Application layer never references ASP.NET Core, so it cannot know that "not found" means 404.

`ResultExtensions` in the Api layer is the single place that mapping happens, which makes the whole service's error-to-status-code policy auditable in about thirty lines.

### FluentValidation in Application

Validators live beside their commands. They are invoked explicitly at the top of each handler rather than through a pipeline behaviour, so the failure path is visible in the handler that owns it and there is no DI decorator to reason about.

### Migrations: Infrastructure is its own startup project

`dotnet ef` requires `Microsoft.EntityFrameworkCore.Design` in the startup project. Passing `--startup-project Catalog.Infrastructure` (rather than the Api) keeps the Api layer free of EF Core entirely. This works because `CatalogDbContextFactory` supplies the design-time context.

```bash
dotnet ef migrations add <Name> \
  --project src/Services/Catalog/Catalog.Infrastructure \
  --startup-project src/Services/Catalog/Catalog.Infrastructure \
  --output-dir Persistence/Migrations
```

## Consequences

- Four projects per service, so ~24 projects across six services plus shared ones. Build times and solution navigation both get worse.
- **For Catalog specifically, this is largely ceremony.** It is CRUD; the layers buy little. The pattern earns its keep in Ordering, where the saga, invariants and compensation logic live, and consistency across services is worth the overhead in the services that do not need it.
- Adding an operation now touches three projects (Application use case, Api endpoint, DI registration) instead of one file.
- The payoff: Application is unit-testable with no database and no HTTP, Domain is testable with nothing at all, and swapping SQL Server for anything else touches one project.
- Two further transitive package pins were needed (`Microsoft.EntityFrameworkCore.Relational` and `.SqlServer` at 10.0.14) because the Aspire integration's floor is 10.0.12 and the version skew produced MSB3277 assembly conflicts.
- Validation error keys are now PascalCase (`"Name"`) rather than camelCase, since FluentValidation reports the property name. Cosmetic, but a visible API change from the previous implementation.
