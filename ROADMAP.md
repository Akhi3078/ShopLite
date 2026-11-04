# .NET Microservices — Roadmap & Project Plan

**Project root:** `C:\DotnetMicroservices` (everything below is built in this folder)
**Companion document:** [ARCHITECTURE.md](ARCHITECTURE.md) — the system design: service boundaries, message catalog, the saga, data and deployment architecture. This file is the *learning path and build sequence*; that one is the *design*.
**Started:** 2026-11-04
**Target:** ShopLite — a learning + portfolio microservices system, run entirely locally, no cloud spend.
**Starting point:** solid ASP.NET Core, new to distributed systems.

---

## Step 0 — Environment setup

Current machine state (checked 2026-11-04):

| Tool | Status |
|---|---|
| .NET SDK 9.0.304 | installed — **end of support 2026-11-10** |
| .NET SDK 11.0.100-rc.3 | installed — release candidate, don't learn on it |
| .NET SDK 10.x (LTS) | **MISSING — install this first** |
| Docker 29.8.2 | installed (engine must be running) |
| git 2.50.0 | installed |
| SQL Server 2022 Developer Edition (16.0.1000.6) | installed and running — full Enterprise feature set, no size cap |
| Instances | `MSSQLSERVER` (default) and `MSSQLSERVER01`, both running |
| SQL Server Browser | **stopped** — needed to reach `MSSQLSERVER01` by name, or specify its port |
| Auth mode | **Windows Authentication only** (`IsIntegratedSecurityOnly=1`) |
| sqlcmd + SSMS | installed |
| Azure CLI (`az`) / `azd` | **not installed** — needed at Phase 7 only |
| Existing databases on default instance | **36** — unrelated work, so prefix everything `ShopLite_` |

- [x] Install the **.NET 10 SDK** — installed 10.0.403 (LTS, supported to 2028-11-14)
- [x] `dotnet tool install -g aspire.cli` — Aspire CLI 13.6.4
- [x] `dotnet tool update -g dotnet-ef` — EF tools must match the runtime major; 9.0.6 was upgraded to 10.0.14
- [ ] Confirm Docker Desktop is actually running (not needed until Phase 2)
- [x] Create `global.json` pinning the SDK, so the .NET 11 RC is never picked up by accident
- [x] `git init` in this folder

Needed later, at Phase 7 (not now):

- [ ] An Azure account (new accounts include $200 credit for 30 days)
- [ ] Azure Developer CLI (`azd`) installed
- [ ] A **billing alert set at $5** before the first deployment

---

## Guiding rules

1. **Learn one distributed concept, then immediately make it hurt in code.** Reading about the dual-write problem teaches nothing; watching inventory desync because a message published but the transaction rolled back teaches it permanently.
2. **Database per service.** Never a shared database, not even temporarily.
3. **Build services 1–3 before scaffolding the rest.** Six half-built services is the most common way this project dies.
4. **Every step ends with a commit and a short ADR** in `docs/adr/` — what was chosen, what was given up. This habit is most of what makes it a portfolio project rather than a folder of tutorial code.

---

## Part 1 — Learning roadmap

Estimates assume ~6–8 hours per week.

### Phase 1 — One service, done right (~1 week)

Not microservices yet. Goal: a single service worth copying five more times.

- [x] Minimal APIs organised as **vertical slices** (feature folders), not Controllers/Services/Repositories layers
- [x] EF Core against the local SQL Server instance + migrations (see "Local SQL Server setup" below)
- [x] Built-in OpenAPI (`Microsoft.AspNetCore.OpenApi`) + Scalar UI — Swashbuckle is no longer the default
- [x] Health checks — understand **liveness vs readiness** as two different endpoints with different meanings
- [x] A `ServiceDefaults` project: the one extension method every service calls for telemetry, health, resilience, discovery
- [x] An Aspire **AppHost** that starts the service via `aspire run` (SQL Server is referenced as an existing connection string, not managed by Aspire — see ADR 0003)

**Checkpoint:** reached 2026-11-04. One command starts everything; dashboard at `https://localhost:17185`, Catalog at `https://localhost:7232`.

Worth reading before moving on: `src/Aspire/ShopLite.ServiceDefaults/Extensions.cs`. It is the whole cross-cutting story in ~130 lines, and `MapDefaultEndpoints` is where the liveness/readiness distinction actually lives — `/alive` filters to checks tagged `"live"`, `/health` requires every check to pass.

### Phase 2 — Two services and synchronous coupling (~1 week)

- [ ] A second service with its **own database**
- [ ] Typed `HttpClient` + service discovery between the two
- [ ] `Microsoft.Extensions.Http.Resilience` (Polly v8): timeout, retry, circuit breaker
- [ ] Understand why retry without idempotency is a bug
- [ ] **YARP** gateway: routing, path transforms, single public entry point
- [ ] Deliberately kill service B, watch service A fail, then fix it with a circuit breaker + fallback

**Checkpoint:** being able to explain why "just call the other service's API" doesn't scale — latency stacking, cascading failure, deployment coupling.

### Phase 3 — Asynchronous messaging (~2 weeks) — THE CORE

This is where microservices actually start. Do not rush it.

- [ ] **Azure Service Bus**, run locally via the Service Bus emulator container + **Wolverine** (use the library; the raw `Azure.Messaging.ServiceBus` client teaches plumbing, not patterns)
- [ ] **Queues vs topics** — point-to-point vs publish/subscribe, and why topics force Standard tier in Azure
- [ ] **Commands vs events** — one recipient with an imperative name, vs many recipients with a past-tense name
- [ ] Integration events in a shared `Contracts` project — and why that project must never contain domain logic
- [ ] **Transactional Outbox** — write the event to your own DB in the same transaction as the state change, publish later. Hand-roll it once before switching to Wolverine's durable SQL Server outbox.
- [ ] **At-least-once delivery** → idempotent consumers, dedupe by message ID
- [ ] Dead-letter queues, retry with backoff, poison messages

**Checkpoint:** being able to describe the dual-write problem and three ways it bites.

### Phase 4 — Distributed data consistency (~2 weeks)

- [ ] **Eventual consistency** — and how to explain it to a product owner as acceptable
- [ ] **Saga / process manager** — a Wolverine saga (persisted to SQL Server) for an order flow spanning four services
- [ ] **Compensating transactions** — there is no rollback, only an apology event
- [ ] CQRS with read models fed by events (lightweight; no event sourcing yet)
- [ ] Event versioning and backward compatibility — the thing that actually breaks real systems

**Checkpoint:** an order can fail at payment and the system unwinds the stock reservation on its own.

### Phase 5 — Cross-cutting concerns (~1.5 weeks)

- [ ] **Keycloak** in a container as identity provider; JWT bearer validation
- [ ] Decide what the gateway validates vs what each service validates
- [ ] Propagate identity across async messages — a genuinely hard problem, worth sitting with
- [ ] **OpenTelemetry** — one trace spanning gateway → service → Service Bus → consumer
- [ ] Structured logging (Serilog) with correlation IDs that survive queue hops
- [ ] Optional: replace the Aspire dashboard with Grafana + Prometheus + Jaeger, to see what Aspire was doing for you

### Phase 6 — Testing (~1 week)

- [ ] Unit tests on domain logic only
- [ ] Integration tests: `WebApplicationFactory` + **Testcontainers** (real SQL Server and the Service Bus emulator per run)
- [ ] Wolverine's test support for handlers and sagas (in-memory transport, tracked sessions)
- [ ] Optional: consumer-driven contract tests (Pact) — understand the idea even if the tooling is skipped

### Phase 7 — Containers and cloud deployment (~2 weeks)

Local first:

- [ ] Multi-stage Dockerfiles, or .NET's built-in SDK container publishing
- [ ] Hand-write `docker-compose.yml` once, to know what Aspire generates

Then Azure (see Part 3 for the architecture and costs):

- [ ] Azure account + `azd` (Azure Developer CLI) installed
- [ ] `aspire publish` / `azd init` → inspect the generated Bicep before running it
- [ ] `azd up` — provisions Container Apps, Container Registry, Azure SQL, Service Bus in one go
- [ ] **Managed Identity** for SQL Server and Service Bus access — no connection strings or passwords in config. This is the single most valuable cloud lesson in the project.
- [ ] Key Vault for the secrets that genuinely remain
- [ ] Application Insights — run a distributed-trace query across services in the portal
- [ ] **KEDA scale rules on Service Bus queue depth** — scale a consumer out under load, and scale to zero when idle
- [ ] `azd down`, then `azd up` again — prove the whole environment is reproducible from source
- [ ] GitHub Actions: build, test, push images, `azd deploy`

### Phase 8 — Hardening (ongoing)

- [ ] Rate limiting
- [ ] API versioning
- [ ] `HybridCache`
- [ ] Outbox cleanup jobs
- [ ] Graceful shutdown
- [ ] Zero-downtime migration strategy
- [ ] Feature flags
- [ ] Idempotency keys on public endpoints

---

## Part 2 — The project: ShopLite

An e-commerce order pipeline. A well-worn example, and that is exactly why it suits a first build: the boundaries are uncontroversial, so the thinking goes into distribution rather than domain modelling. It also produces a saga naturally, which is the hardest item on the roadmap.

### Services, and why each one exists pedagogically

| Service | Storage | Teaches | Milestone |
|---|---|---|---|
| **Catalog** | SQL Server | Baseline service, read-heavy, caching | 1 |
| **Basket** | Redis | Polyglot persistence — no relational DB at all | 2 |
| **Ordering** | SQL Server | DDD-lite, outbox, saga orchestration | 3 |
| **Inventory** | SQL Server | Reservations + compensating actions | 4 |
| **Payment** | SQL Server | Deliberate failures, retries, idempotency | 4 |
| **Notification** | none | A pure event consumer, no API at all | 5 |
| **Gateway** (YARP) | — | Single entry point, auth boundary | 2 |
| **Keycloak** | container | Identity, token propagation | 5 |

### What kind of project this is, concretely

A **single C# solution of ~13 .NET projects**, orchestrated locally by Aspire. No frontend — the system is exercised with `.http` files, the Scalar UI, and the Aspire dashboard. A Blazor/React UI is a Phase 8 extra, deliberately out of scope: it teaches nothing about distribution and doubles the surface area.

| Project | .NET template | What it is |
|---|---|---|
| Catalog.Api, Basket.Api, Ordering.Api, Inventory.Api, Payment.Api | `dotnet new webapi` | ASP.NET Core Web APIs, Minimal API style — no controllers |
| Notification.Worker | `dotnet new worker` | A `BackgroundService` with **no HTTP endpoint at all** — pure message consumer |
| ShopLite.Gateway | `dotnet new web` + YARP | Empty ASP.NET Core host, config-driven reverse proxy |
| ShopLite.AppHost | `dotnet new aspire-apphost` | Orchestrator — declares every service + SQL Server, Redis, Service Bus, Keycloak |
| ShopLite.ServiceDefaults | `dotnet new aspire-servicedefaults` | Class library every service references |
| Contracts, Shared, Ordering.Domain | `dotnet new classlib` | Plain class libraries |
| Catalog.Tests, Ordering.Tests, IntegrationTests | `dotnet new xunit` | Test projects |

Nothing exotic: Web APIs, one worker, one orchestrator. The learning is not in the project types — it is in what happens *between* them.

### The domain scope

**An online store, narrowed to one slice: checkout and order fulfilment.** Not a whole e-commerce platform — no search, reviews, recommendations, shipping carriers or returns. Just the path from "customer has items in a basket" to "order is paid and confirmed", because that path crosses four service boundaries and has to survive failure partway through.

| Service | Entities it owns |
|---|---|
| Catalog | `Product` — name, description, price, category |
| Basket | `Basket` per customer — items with product ID, quantity, price snapshot (Redis, with TTL) |
| Ordering | `Order`, `OrderItem`, `OrderStatus`, `OrderSagaState` |
| Inventory | `StockItem` (available vs reserved), `Reservation` per order |
| Payment | `PaymentAttempt` — amount, status, idempotency key |
| Notification | nothing — stateless consumer, writes a fake email to the log |

Two deliberate modelling decisions, both of which tutorials commonly get wrong by putting `StockQuantity` on `Product`:

- **Stock lives in Inventory, not Catalog.**
- **Basket stores a price snapshot** rather than calling Catalog at checkout.

These are the first real bounded-context calls to make.

### The flow being built

Happy path:

```
POST /basket/checkout
  Basket       -> publishes BasketCheckedOut
  Ordering     -> creates Order (Submitted), starts saga, sends ReserveStock
  Inventory    -> reserves stock, publishes StockReserved
  Ordering     -> sends ProcessPayment
  Payment      -> succeeds, publishes PaymentSucceeded
  Ordering     -> Order -> Completed, publishes OrderCompleted
  Inventory    -> converts reservation to a deduction
  Notification -> "your order is confirmed"
```

Failure path — the reason this domain was chosen:

```
  Payment      -> declines, publishes PaymentFailed
  Ordering     -> saga compensates: sends ReleaseStock
  Inventory    -> releases the reservation
  Ordering     -> Order -> Cancelled
  Notification -> "payment failed"
```

That compensation branch is the whole curriculum in miniature: no distributed transaction, no rollback, just events and a state machine that must stay correct even when Payment crashes *after* charging the card but *before* publishing. Phase 4 is where that case gets handled.

### Repo layout

```
C:\DotnetMicroservices\
  global.json
  ShopLite.sln
  src/
    Aspire/
      ShopLite.AppHost/            # orchestration: services + SQL Server + Redis + Service Bus
      ShopLite.ServiceDefaults/    # telemetry, health, resilience, discovery
    BuildingBlocks/
      ShopLite.Contracts/          # integration events ONLY. no logic, no EF.
      ShopLite.Shared/             # Result types, pagination, exception handling
    Services/
      Catalog/      Catalog.Api/
      Basket/       Basket.Api/
      Ordering/     Ordering.Api/  Ordering.Domain/
      Inventory/    Inventory.Api/
      Payment/      Payment.Api/
      Notification/ Notification.Worker/
    Gateway/
      ShopLite.Gateway/
  tests/
    Catalog.Tests/
    Ordering.Tests/
    ShopLite.IntegrationTests/
  docs/
    adr/                           # one short file per decision
```

Two layout rules worth holding:

- `Contracts` is the **only** project services share.
- A service gets extra projects (`.Domain`, `.Application`) only where the logic earns it — Ordering will, Catalog won't.

### Local SQL Server setup

Using the installed instance rather than a container. Decisions:

**Instance:** the default instance, `localhost`. (`MSSQLSERVER01` would isolate ShopLite from the 36 existing databases, but needs SQL Server Browser started or a fixed port — not worth the friction.)

**One database per service, on the shared instance:**

| Database | Service | Also holds |
|---|---|---|
| `ShopLite_Catalog` | Catalog.Api | — |
| `ShopLite_Ordering` | Ordering.Api | Wolverine outbox + saga tables |
| `ShopLite_Inventory` | Inventory.Api | Wolverine outbox tables |
| `ShopLite_Payment` | Payment.Api | Wolverine outbox tables |

Basket uses Redis; Notification.Worker holds no state. The `ShopLite_` prefix matters — these sit alongside 36 unrelated databases, and you will want to find and drop them cleanly later.

**Separate databases on one instance is a legitimate middle ground**, not a compromise of the pattern. What makes database-per-service real is the *rule*, not the physical separation:

- One `DbContext` per service, one migrations history table per database
- **No cross-database queries, ever** — no three-part names like `ShopLite_Catalog.dbo.Products` from Ordering
- Data crosses a boundary only as a message or an HTTP response

**The outbox must live in the service's own database.** This is not a style choice: the point of the outbox is that the state change and the event insert share one transaction. A shared or separate messaging database destroys the guarantee and leaves you with the dual-write problem you were trying to solve.

**Enforcing the boundary (recommended at Phase 3):** your Windows account can see every database, so the no-cross-database rule is pure discipline right now. Switching the instance to Mixed Mode auth and creating one SQL login per service — each `db_owner` on its own database only — makes cheating *impossible* rather than merely discouraged. It also makes local connection strings resemble the Azure ones. Requires a server setting change plus a service restart.

**Two feature traps, because this is Developer Edition:**

- Developer Edition exposes the full Enterprise feature set. Azure SQL does not support all of it (SQL Server Agent, cross-database queries, FILESTREAM, and others). Do not build on a feature locally that will not exist in Phase 7.
- Cross-database queries are *impossible* in Azure SQL. The discipline above is therefore not optional — it is what the cloud will enforce for you anyway.

**Keep the container path alive:** integration tests use Testcontainers so CI works without your machine, and the AppHost keeps a container option behind a launch profile. The local instance is for the daily dev loop — faster startup, no 2 GB of container RAM, SSMS and sqlcmd already pointed at it, data surviving reboots.

### Technology decisions, settled

| Concern | Choice | Note |
|---|---|---|
| Runtime | .NET 10 LTS | pinned in `global.json` |
| Local orchestration | .NET Aspire 13.x | replaces hand-rolled compose for dev |
| Messaging broker | Azure Service Bus | emulator container locally, Standard tier in Azure |
| Messaging library | **Wolverine** (MIT) | see the licensing note below — this replaces MassTransit |
| Relational data | SQL Server + EF Core | local Developer Edition instance for dev, Testcontainers for tests, Azure SQL in cloud |
| Cloud hosting | Azure Container Apps | `azd up` from the Aspire AppHost |
| Cloud secrets | Key Vault + Managed Identity | no connection strings in config |
| Cloud telemetry | Application Insights | same OpenTelemetry code as local |
| Cache / Basket | Redis | |
| Gateway | YARP | |
| Identity | Keycloak | free, container, realistic OIDC |
| Resilience | `Microsoft.Extensions.Http.Resilience` | Polly v8 |
| Observability | OpenTelemetry → Aspire dashboard | Grafana stack later |
| Validation | FluentValidation | |
| Testing | xUnit + Testcontainers + Wolverine test support | |
| API docs | built-in OpenAPI + Scalar | not Swashbuckle |

**Licensing traps — the .NET ecosystem shifted here in 2025–26, and most tutorials predate it:**

- **MediatR and AutoMapper** moved to commercial licensing. Skip both. Hand-written mapping is clearer; if the mediator pattern is wanted, write the ~30 lines by hand.
- **MassTransit v9 is commercially licensed** (reported around $400/month), and v8 — still Apache 2.0 — is in maintenance with security patches committed only through roughly the end of 2026. Since that window is closing now, **this project uses Wolverine (MIT)** instead. It supports the Azure Service Bus transport, a durable SQL Server outbox, and sagas.
- Trade-off being accepted: MassTransit v8 has more tutorials and StackOverflow answers. Wolverine has fewer, but is the one with a future. Rebus (MIT) is the other free option if Wolverine disappoints.
- To confirm at implementation time: the exact Wolverine package set for Azure Service Bus + SQL Server saga persistence (`WolverineFx.AzureServiceBus`, `WolverineFx.SqlServer`, `WolverineFx.EntityFrameworkCore`) against current docs, rather than trusting this plan.

### Build order

- [x] 1. `global.json`, solution, Aspire AppHost + ServiceDefaults, `git init`, first commit
- [x] 2. Catalog.Api end to end — SQL Server, migrations, CRUD, OpenAPI, health checks, visible in the dashboard
- [ ] 3. Gateway in front of Catalog
- [ ] 4. Basket.Api on Redis; Basket calls Catalog synchronously to price items → add resilience, then break it on purpose
- [ ] 5. Service Bus emulator into the AppHost; Basket publishes `BasketCheckedOut`; Ordering consumes it
- [ ] 6. Ordering's outbox, hand-rolled first
- [ ] 7. Inventory + Payment, then the saga coordinating all four with compensation
- [ ] 8. Keycloak, auth at the gateway, token propagation
- [ ] 9. Testcontainers integration tests
- [ ] 10. Dockerfiles, compose, CI

### Definition of done

- [ ] `aspire run` starts everything
- [ ] A checkout produces an order, reserves stock, takes payment, and sends a notification
- [ ] Killing the Payment container mid-flow leaves no orphaned stock reservation
- [ ] One trace in the dashboard spans the full flow across the queue
- [ ] Integration tests pass against real containers in CI

---

## Part 3 — Cloud (Azure) architecture

### The principle: one AppHost, two environments

Develop locally against containers and emulators; deploy the same AppHost to Azure. Aspire's Azure integrations are built for exactly this — the resource is *declared* once, and where it actually lives depends on whether you ran `aspire run` or `azd up`:

```csharp
var sql = builder.AddAzureSqlServer("sql").RunAsContainer();      // container locally, Azure SQL on publish
var sb  = builder.AddAzureServiceBus("messaging").RunAsEmulator(); // emulator locally, real namespace on publish
```

No `#if DEBUG`, no parallel compose file, no second set of connection strings. This is the mechanism that makes "full-fledged running project" achievable without maintaining two systems.

### Local vs cloud, per concern

| Concern | Local (`aspire run`) | Azure (`azd up`) |
|---|---|---|
| Relational data | local SQL Server 2022 instance | Azure SQL Database (free offer) |
| Messaging | Service Bus emulator container | Azure Service Bus, Standard tier |
| Cache / Basket | Redis container | Redis as a container app |
| Identity | Keycloak container | Keycloak as a container app |
| Compute | local processes | Azure Container Apps |
| Images | local Docker | Azure Container Registry (Basic) |
| Secrets | `dotnet user-secrets` | Key Vault + **Managed Identity** |
| Telemetry | Aspire dashboard | Application Insights |

### What migrating to Azure actually looks like

| Local | Becomes in Azure |
|---|---|
| 4 databases on `localhost` | 4 Azure SQL databases on one logical server (free offer covers 10) |
| Windows Authentication | **Managed Identity** via Entra ID — also passwordless, same concept, different connection string |
| Service Bus emulator container | a real Service Bus Standard namespace |
| Redis container | Redis as a container app |
| `aspire run` processes | Azure Container Apps |
| Aspire dashboard | Application Insights, including the **Application Map** service-dependency graph |
| nothing | Bicep templates, generated by Aspire and read by you |
| nothing | KEDA scale rules on queue depth |

EF Core migrations carry over unchanged — same provider, same `dotnet ef database update`, different connection string. That is the main practical payoff of using real SQL Server locally instead of a different engine.

Note the auth progression is conceptually clean: **Windows auth locally and Managed Identity in Azure are both passwordless integrated authentication.** Neither stores a password in config. You are not learning a throwaway local pattern.

### What the Azure account buys you, in learning terms

Five things that cannot be learned locally at any price:

1. **The emulator-vs-real gap.** The Service Bus emulator is unsupported and incomplete; you only find out which of your assumptions were emulator artifacts by deploying.
2. **Managed Identity / passwordless cloud auth.** No local equivalent, and the single most review-worthy thing in the repo.
3. **Scale-to-zero and cold starts.** Real distributed behaviour — a first request that takes 8 seconds because the consumer and the database were both asleep.
4. **Cost as an architectural constraint.** The reason Basket uses Redis and Payment does not get its own Service Bus namespace.
5. **A live URL.** For a portfolio project, a reviewer clicking a working Scalar page beats a README full of claims.

And what it does *not* help with: **do not develop against it.** Phases 1–6 stay local. Cloud round-trips are slow, cost money, and teach less per hour than breaking a container on purpose.

### Costs, honestly

The project was scoped as "no cloud spend"; adding Azure changes that. Verified free grants and the one real cost:

| Service | Free allowance | Cost if exceeded |
|---|---|---|
| Azure SQL Database | **10 databases** per subscription, each 100,000 vCore-seconds + 32 GB/month | standard General Purpose serverless rates |
| Azure Container Apps | 180,000 vCPU-seconds, 360,000 GiB-seconds, 2M requests per subscription per month | consumption rates |
| Application Insights | 5 GB/month ingestion | per-GB |
| Key Vault | effectively free at this scale | ~$0.03 per 10k operations |
| **Azure Service Bus** | **no free tier** | Standard ≈ **$10/month** base (reported ~$0.0134/hour), includes first 13M operations |
| Container Registry | none | Basic ≈ **$5/month** |

Realistic total: **roughly $15/month if left running continuously**, close to $0 if torn down between sessions. A new Azure account also includes $200 of credit for the first 30 days.

The discipline that keeps this cheap: `azd up` → verify → screenshot/record → `azd down`. Redeploying is one command, so there is no reason to leave a portfolio project running. Set a **billing alert at $5** before the first `azd up`, not after.

Note the free SQL offer fits database-per-service unusually well: **10 free databases, and only 5 services need one.** The constraint that normally pushes beginners toward a shared database doesn't apply here.

### Caveats worth knowing before relying on any of this

- **The Service Bus emulator is dev/test only and not officially supported** by Microsoft (launched Nov 2024, issues go to GitHub). It lacks Entra ID auth, virtual networks, activity logs, partitioned entities, and the portal UI. Local behaviour therefore differs from cloud at the edges — expect at least one surprise on first deploy, and treat that debugging as part of the curriculum.
- **Azure SQL's free offer is serverless with auto-pause.** The first query after an idle period is slow (cold start). Fine for a demo; alarming if unexpected.
- **Service Bus Basic tier has no topics**, so publish/subscribe requires Standard. That is why Standard is the floor rather than Basic.
- Use **AMQP**, not SBMP — the older protocol was retired in September 2026. Modern SDK versions default to AMQP, so this is mainly a warning about following old tutorials.
- Prefer **Managed Identity over connection strings** for SQL and Service Bus. It is slightly more setup and is the thing that distinguishes this from a tutorial project in a review.

### Cloud concepts this adds to the curriculum

Managed Identity and passwordless auth · Bicep / infrastructure-as-code (generated, then read) · scale-to-zero and cold starts · KEDA autoscaling on queue depth · per-service database provisioning · cloud cost as an architectural constraint · the gap between an emulator and the real service.

---

## Concepts to be able to explain out loud

Bounded context · database per service · the dual-write problem · transactional outbox · at-least-once delivery and idempotency · CAP and eventual consistency · saga vs two-phase commit · CQRS · service discovery · circuit breaker / retry / timeout / bulkhead · liveness vs readiness · distributed tracing · backward-compatible event versioning.

## Anti-patterns to be able to name

Shared database · distributed monolith (one change touches five services) · nano-services · chatty synchronous chains · shared domain libraries · "we'll add observability later" · entity services (an `OrderService` that is just CRUD over a table is not a bounded context).

---

## Honest caveat

For a real greenfield product this architecture is the wrong starting point — a modular monolith with clean internal boundaries is. ShopLite exists to surface the distributed problems and their costs. Knowing **when not to** reach for microservices is part of what is being learned here, and it is a frequent interview question.

---

## References

- [.NET Support Policy](https://dotnet.microsoft.com/platform/support/policy)
- [.NET 8 and .NET 9 end of support — 2026-11-10](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/)
- [.NET Aspire 13.1 release coverage](https://www.infoq.com/news/2026/01/dotnet-aspire-13-1-release)
