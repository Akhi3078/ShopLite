# 3. Catalog service shape

**Date:** 2026-11-04
**Status:** Accepted

## Context

Catalog is the first service, so its structure becomes the template the other five are copied from. Decisions here are worth more scrutiny than their size suggests.

## Decisions

### Vertical slices, not layers

Each operation is one file under `Features/Products/` holding its route, request and response shapes, and handler. There is no `IProductRepository`, no `ProductService`, no `ProductDto` shared across operations.

A layered structure spreads one change across four folders and encourages reuse between operations that only look similar. A slice is read top to bottom and deleted in one piece.

### The entity guards its own state

`Product` has private setters, a private constructor for EF Core, and a `Create`/`Update` pair. Handlers cannot assemble an invalid product field by field.

### Reads are projected, writes are tracked

Query handlers use `AsNoTracking()` and `Select` straight into a response record, so no entity is materialised. Update loads the tracked entity, because the entity's own method must run.

### Version 7 GUIDs for keys

`Guid.CreateVersion7()` is time-ordered, so inserts append to the clustered index. `Guid.NewGuid()` scatters them, causing page splits and index fragmentation on SQL Server at volume.

### Validation is hand-rolled, for now

`ProductValidation` is a plain static class. FluentValidation is the eventual choice but earns its place only once rules become conditional or cross-field. Nothing outside the slice depends on how errors are produced, only on their shape, so the swap stays contained.

### Migrations are applied explicitly

`dotnet ef database update`, never `Database.Migrate()` on startup. Automatic migration on boot is convenient at this size and is precisely the habit that makes zero-downtime deployment hard later. A design-time factory (`CatalogDbContextFactory`) exists so the tooling does not need to boot the application — including its seeder — merely to read the model.

### SQL Server is referenced, not managed

The AppHost uses `AddConnectionString("catalogdb")` rather than `AddSqlServer(...)`, because the database lives on the developer's existing local instance. Aspire injects and displays it but does not own its lifecycle. Moving to a container is a one-line change.

## Consequences

- Adding an operation means adding one file and one line in `ProductEndpoints`.
- There is no shared abstraction over the DbContext, so a future need to swap persistence would touch every slice. Accepted: that need is hypothetical, and the indirection to guard against it has a certain cost.
- The connection string is duplicated between the AppHost configuration and the design-time factory's fallback. Accepted and documented; it is a local development default with no secret in it.
- Because the local instance is not managed by Aspire, a fresh clone requires `dotnet ef database update` before the service is healthy. Noted in the README.
