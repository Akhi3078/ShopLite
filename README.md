# ShopLite

A .NET microservices implementation of an e-commerce checkout and order-fulfilment pipeline, built as a learning and portfolio project.

- **[ARCHITECTURE.md](ARCHITECTURE.md)** — system design: service boundaries, message catalog, the fulfilment saga, data and deployment architecture
- **[ROADMAP.md](ROADMAP.md)** — learning path and build sequence, with progress checkboxes
- **[docs/adr/](docs/adr/)** — architecture decision records

## Prerequisites

| | |
|---|---|
| .NET SDK | **10.0.403** or later 10.x (pinned in `global.json`) |
| Aspire CLI | `dotnet tool install -g aspire.cli` |
| EF Core CLI | `dotnet tool install -g dotnet-ef` (must be 10.x) |
| Node.js | 24.x, with npm |
| SQL Server | a local instance reachable at `localhost` |
| Docker | required from Phase 3 onward (Redis, Service Bus emulator) |

## Running

First time only — create the two databases:

```bash
dotnet ef database update --project src/Services/Catalog/Catalog.Infrastructure  --startup-project src/Services/Catalog/Catalog.Infrastructure
dotnet ef database update --project src/Services/Identity/Identity.Api           --startup-project src/Services/Identity/Identity.Api
```

Install the UI's dependencies once:

```bash
cd src/Web/shoplite-web && npm install
```

Then, from the repository root:

```bash
aspire run
```

That starts both APIs and the Angular dev server, and opens the Aspire dashboard.

| What | Where |
|---|---|
| **Web UI** | http://localhost:4200 |
| Aspire dashboard | https://localhost:17185 |
| Identity API reference | https://localhost:7130/scalar/v1 |
| Catalog API reference | https://localhost:7232/scalar/v1 |
| Ready-made requests | [Catalog.Api.http](src/Services/Catalog/Catalog.Api/Catalog.Api.http) |

### Sign in

Three accounts are seeded in Development. All use the password `Passw0rd!`.

| Email | Role | Can do |
|---|---|---|
| `admin@shoplite.local` | Admin | Everything, including managing users and roles |
| `manager@shoplite.local` | Manager | Create, edit and delete products |
| `customer@shoplite.local` | Customer | Browse the catalog, read-only |

Registering through the UI always creates a Customer. Roles are granted by an Admin on
the Users screen.

Because roles live in the JWT, a role change takes effect only after that user signs in
again — see [ADR 0005](docs/adr/0005-identity-and-angular-ui.md).

### Notes

The dashboard needs no login token, because `ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS`
is set in the AppHost launch profile. That is a local-development convenience only — the
dashboard shows logs, traces, environment variables and connection strings, so the token
must come back before it is reachable from anywhere but this machine.

Migrations are applied explicitly, never on startup — see
[ADR 0003](docs/adr/0003-catalog-service-shape.md).

## Repository layout

```
src/Aspire/ShopLite.AppHost/          orchestration — declares every service and dependency
src/Aspire/ShopLite.ServiceDefaults/  telemetry, health, resilience, auth policies, CORS
src/Services/Catalog/                 Domain / Application / Infrastructure / Api
src/Services/Identity/                users, roles, JWT issuing (single project — ADR 0005)
src/Web/shoplite-web/                 Angular 22 + Bootstrap 5 UI
docs/adr/                             architecture decision records
```

## Access control

Three layers, deliberately redundant — but only one of them is security:

| Layer | Where | Purpose |
|---|---|---|
| Navigation | `@if` in Angular templates | Don't offer what would be refused |
| Routing | `roleGuard()` on routes | Don't load a page that cannot work |
| **API** | `.RequireAuthorization()` | **The actual enforcement** |

The first two are convenience for honest users. Anything in a browser can be edited, so
the server-side check is the real boundary.

## Status

Phase 1 complete, plus identity and UI. See [ROADMAP.md](ROADMAP.md) for what's next.
