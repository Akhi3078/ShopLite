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
| SQL Server | a local instance reachable at `localhost` (Catalog and Identity only) |
| Docker | **required** — Redis and RabbitMQ containers, both started by Aspire |

## Running

First time only — create the three SQL databases (Basket uses Redis, which Aspire starts for you):

```bash
dotnet ef database update --project src/Services/Catalog/Catalog.Infrastructure --startup-project src/Services/Catalog/Catalog.Infrastructure
dotnet ef database update --project src/Services/Identity/Identity.Api          --startup-project src/Services/Identity/Identity.Api
dotnet ef database update --project src/Services/Ordering/Ordering.Infrastructure --startup-project src/Services/Ordering/Ordering.Infrastructure
```

Install the UI's dependencies once:

```bash
cd src/Web/shoplite-web && npm install
```

Then, from the repository root:

```bash
aspire run
```

That starts the three APIs, the YARP gateway, and the Angular dev server, and opens the Aspire dashboard.

| What | Where |
|---|---|
| **Web UI** | http://localhost:4200 |
| Aspire dashboard | https://localhost:17185 |
| API gateway | https://localhost:7260 (`/identity`, `/catalog`, `/basket`, `/ordering`) |
| Identity API reference | https://localhost:7260/identity/scalar/v1 |
| Catalog API reference | https://localhost:7260/catalog/scalar/v1 |
| Basket API reference | https://localhost:7290/scalar/v1 (direct — see note) |
| Ready-made requests | [Catalog.Api.http](src/Services/Catalog/Catalog.Api/Catalog.Api.http) |

### Sign in

Three accounts are seeded in Development. All use the password `Passw0rd!`.

| Email | Role | Can do |
|---|---|---|
| `admin@shoplite.local` | Admin | Everything, including managing users and roles |
| `manager@shoplite.local` | Manager | Create, edit and delete products |
| `customer@shoplite.local` | Customer | Browse the catalog, manage their own basket |

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

Basket's Scalar page is not reachable through the gateway. Identity and Catalog expose
their resources at the root, so the gateway strips the `/identity` and `/catalog` prefixes;
Basket's own resource is already `/basket`, so its route forwards unchanged and there is no
prefix to strip. Use the direct URL above.

### Watching a message travel

Check out a basket, and the order appears a moment later:

1. Add something to the basket, then press **Check out**. The API answers `202 Accepted` —
   the basket is emptied and an event is recorded, but no order exists yet.
2. The orders screen says the order is on its way, because it genuinely might not be there.
3. Refresh. The order appears with status **AwaitingFulfilment**.

That gap is eventual consistency, and it is the point of the phase rather than a defect.
See [ADR 0008](docs/adr/0008-asynchronous-messaging.md).

### Looking inside Basket's store

Basket keeps baskets, fallback prices and idempotency markers in Redis. RedisInsight is
wired up in the AppHost, so the dashboard links straight to a browser UI for it.

From the command line, note that Aspire starts Redis with a generated password and TLS on
6379, so `redis-cli` needs the **plaintext port 6380** and `-a`:

```bash
C=$(docker ps --filter name=basketcache --format '{{.Names}}')
PW=$(docker inspect $C --format '{{range .Config.Env}}{{println .}}{{end}}' | grep ^REDIS_PASSWORD= | cut -d= -f2)
docker exec $C redis-cli -p 6380 -a "$PW" --no-auth-warning --scan
```

Three key shapes, each with a TTL — `basket:{customerId}` (30 days, refreshed on write),
`price:{productId}` (24h) and `idem:{requestId}` (24h). See
[ADR 0007](docs/adr/0007-basket-on-redis.md).

### Seeing the resilience pipeline work

Stop Catalog in the Aspire dashboard, then add an item to the basket. The first attempt
takes several seconds while retries time out; once the circuit opens, later attempts return
in milliseconds with `"priceFromCache": true`. Adding a product Basket has never priced
returns 503 rather than inventing a price. Start Catalog again and `priceFromCache` goes
back to `false`. The reasoning is in
[ADR 0006](docs/adr/0006-basket-service-and-synchronous-coupling.md).

## Repository layout

```
src/Aspire/ShopLite.AppHost/          orchestration — declares every service and dependency
src/Aspire/ShopLite.ServiceDefaults/  telemetry, health, resilience, auth policies, CORS
src/Gateway/ShopLite.Gateway/         YARP reverse proxy — single API entry point
src/Services/Catalog/                 Domain / Application / Infrastructure / Api
src/Services/Identity/                users, roles, JWT issuing (single project — ADR 0005)
src/Services/Basket/                  Domain / Application / Infrastructure / Api — on Redis, calls Catalog (ADR 0006, 0007)
src/Services/Ordering/                Domain / Application / Infrastructure / Api — consumes messages (ADR 0008)
src/Shared/ShopLite.Contracts/        message records — the ONLY project shared between services
src/Web/shoplite-web/                 Angular 22 + Bootstrap 5 UI — required, not optional
docs/adr/                             architecture decision records
```

## The web UI

The Angular app is a **required part of this project**, not a demo harness. Every service
feature is expected to be reachable from it; anything that only works from `curl` or a
`.http` file is unfinished.

| Screen | Route | Who sees it |
|---|---|---|
| Sign in / Register | `/login`, `/register` | anyone |
| Products | `/products` | any signed-in user |
| New / Edit product | `/products/new`, `/products/:id/edit` | Admin, Manager |
| Basket | `/basket` | any signed-in user |
| Orders | `/orders` | any signed-in user (Admin sees everyone) |
| Users and roles | `/users` | Admin |

It is also where the distributed behaviour becomes visible rather than theoretical:

- A price Basket served from its **fallback cache** is badged on the line, warned about
  above the table, and marked in the navbar. A stale price is never shown as current.
- A **503** from an unreachable Catalog says exactly that, instead of a generic failure.
- Adding to the basket sends an **`Idempotency-Key`**, so a retried click cannot add the
  quantity twice.
- A **role change** only takes effect at next sign-in, because roles live in the token.

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

Phases 1, 2 and 3 complete, plus identity and UI.

Four services behind one gateway: Identity, Catalog, Basket and Ordering. Three SQL
databases, plus Redis and RabbitMQ containers. One synchronous service call
(Basket → Catalog, with a circuit breaker and a cached fallback) and one asynchronous
flow (Basket → `BasketCheckedOut` → Ordering, with an outbox and idempotent consumers).

Orders stay **AwaitingFulfilment**: reserving stock and taking payment, plus the saga that
coordinates and compensates them, are Phase 4.

Test coverage is one regression test (`ng test` in `src/Web/shoplite-web`), written to pin
a bug rather than as the start of a suite. The messaging in particular was verified by
hand, which is the weakest part of the project — the real testing work is Phase 6.

See [ROADMAP.md](ROADMAP.md) for what's next — Phase 3 is asynchronous messaging, and
needs Docker.
