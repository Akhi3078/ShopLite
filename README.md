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
| SQL Server | a local instance reachable at `localhost` |
| Docker | required from Phase 2 onward (Redis, Service Bus emulator, Keycloak) |

## Running

First time only — create the databases:

```bash
dotnet ef database update --project src/Services/Catalog/Catalog.Infrastructure --startup-project src/Services/Catalog/Catalog.Infrastructure
```

Then:

```bash
aspire run
```

This starts every service and opens the Aspire dashboard.

| What | Where |
|---|---|
| Aspire dashboard | https://localhost:17185 |
| Catalog API reference | https://localhost:7232/scalar/v1 |
| Ready-made requests | [Catalog.Api.http](src/Services/Catalog/Catalog.Api/Catalog.Api.http) |

The dashboard needs no login token, because `ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS`
is set in the AppHost launch profile. That is a local-development convenience only — the
dashboard shows logs, traces, environment variables and connection strings, so the token
must come back before it is reachable from anywhere but this machine.

Migrations are applied explicitly, never on startup — see
[ADR 0003](docs/adr/0003-catalog-service-shape.md).

## Repository layout

```
src/Aspire/ShopLite.AppHost/          orchestration — declares every service and dependency
src/Aspire/ShopLite.ServiceDefaults/  telemetry, health checks, service discovery, resilience
src/Services/                         one folder per service
src/Gateway/                          YARP reverse proxy
tests/                                unit and integration tests
docs/adr/                             architecture decision records
```

## Status

Under construction. See [ROADMAP.md](ROADMAP.md) for the current phase.
