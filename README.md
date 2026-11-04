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

```bash
aspire run
```

This starts every service and its dependencies, and opens the Aspire dashboard.

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
