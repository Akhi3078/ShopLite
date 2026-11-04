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
dotnet ef database update --project src/Services/Catalog/Catalog.Api
```

Then:

```bash
aspire run
```

This starts every service and opens the Aspire dashboard. Catalog's API reference is at
`https://localhost:7232/scalar/v1`, and `src/Services/Catalog/Catalog.Api/Catalog.Api.http`
holds ready-made requests.

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
