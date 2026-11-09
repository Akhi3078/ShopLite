# ShopLite — Architecture

**Status:** target architecture. Implementation is in progress; build sequence and progress are tracked in [ROADMAP.md](ROADMAP.md).
**Last updated:** 2026-11-04

---

## 1. Overview

ShopLite is a microservices implementation of an e-commerce **checkout and order-fulfilment pipeline**. It deliberately covers one narrow slice of a store — the path from a populated basket to a paid, confirmed order — rather than a broad feature set.

That slice is the point. It crosses five service boundaries, has to remain correct when any participant fails midway, and therefore exercises the problems that distinguish distributed systems from a monolith with more projects in it: eventual consistency, the dual-write problem, idempotent delivery, and distributed rollback via compensation.

A role-based Angular UI is a **required part of the system**, not a demo harness bolted on at the end. It is what makes the access-control rules observable rather than described, and it is where the distributed failure modes become visible to a person: a degraded price, a rejected write, a stale role. Every service feature is expected to be reachable through it.

Out of scope by design: product search, reviews, recommendations, shipping carriers and returns.

---

## 2. Architectural principles

1. **Each service owns its data exclusively.** No other service reads its tables. Data crosses a boundary only as an HTTP response or a message.
2. **Asynchronous by default.** Synchronous HTTP is used only where the caller genuinely needs an answer before it can respond. Everything in the fulfilment pipeline is message-driven.
3. **Eventual consistency is accepted, not worked around.** There is no distributed transaction anywhere in the system.
4. **Failure is a modelled state, not an exception path.** Every step that can fail has a defined compensating action.
5. **At-least-once delivery is assumed.** Every consumer is idempotent; no consumer may rely on being invoked exactly once.
6. **Contracts are explicit and versioned.** Integration events are a published interface, treated with the same care as a public API.
7. **One definition of the system.** The Aspire AppHost declares every service and dependency, and the same declaration drives both local development and cloud deployment.

---

## 3. Layers

A distinction worth stating plainly, because the two are easy to conflate:

```
   application code          handlers, endpoints, domain logic
          │
   ───────┼──────────────────────────────────────────────────
          ▼
   Wolverine                 messaging LIBRARY
                             routing · outbox · sagas · retries · serialization
          │
   ───────┼──────────────────────────────────────────────────
          ▼
   Azure Service Bus         messaging BROKER
                             queues · topics · dead-letter · delivery
```

The broker is infrastructure; the library is a dependency of the application. They are chosen independently — Wolverine supports RabbitMQ, Amazon SQS and Kafka equally well, and swapping brokers is a configuration change rather than a rewrite.

---

## 4. System context

```
                       ┌──────────────────┐
                       │     Customer     │
                       │  (Angular SPA)   │
                       └────────┬─────────┘
                                │ HTTPS
                       ┌────────▼──────────┐       ┌──────────────┐
                       │  ShopLite.Gateway │──────►│ Identity.Api │
                       │      (YARP)       │  JWT  │ users, roles │
                       └────────┬──────────┘       └──────┬───────┘
                                │                         │
                    ┌───────────┴────────────┐    ShopLite_Identity
                    │     ShopLite services  │
                    │  (see §5 and §6, §7)   │
                    └────────────────────────┘
```

The gateway is the only publicly reachable component. No service is exposed directly.

---

## 5. Service catalog

| Service | Responsibility | Data store | Public API |
|---|---|---|---|
| **shoplite-web** | Angular SPA: the system's user interface | browser storage only | **is the client** |
| **ShopLite.Gateway** | Single entry point: routing, JWT validation, rate limiting | none | yes |
| **Identity.Api** | Users, roles, JWT issuing | `ShopLite_Identity` | via gateway |
| **Catalog.Api** | Product information and pricing | `ShopLite_Catalog` | via gateway |
| **Basket.Api** | Per-customer basket; initiates checkout | Redis | via gateway |
| **Ordering.Api** | Order lifecycle; owns the fulfilment saga (saga in Phase 4) | `ShopLite_Ordering` | via gateway |
| **Inventory.Api** | Stock levels and reservations | `ShopLite_Inventory` | internal only |
| **Payment.Api** | Payment attempts against a simulated gateway | `ShopLite_Payment` | internal only |
| **Notification.Worker** | Customer notifications | none (stateless) | **no HTTP endpoint** |

Notification.Worker is intentionally endpoint-free: it exists to demonstrate that a service need not be a web API.

### Entity ownership

| Service | Aggregates / entities |
|---|---|
| Catalog | `Product` |
| Basket | `Basket`, `BasketItem` (with price snapshot) |
| Ordering | `Order`, `OrderItem`, `OrderSagaState` |
| Inventory | `StockItem` (available / reserved), `Reservation` |
| Payment | `PaymentAttempt` |

Two boundary decisions that are commonly got wrong:

- **Stock belongs to Inventory, not Catalog.** `Product` has no quantity field. Availability and description change for different reasons, at different rates, under different load.
- **Basket stores a price snapshot.** Checkout does not re-query Catalog. The price the customer saw is the price in the order.

### Internal structure of a service

Every service is four projects following Clean Architecture, with the dependency direction pointing inward:

```
  ┌────────────────────────────────────────────────┐
  │  Api               HTTP · OpenAPI · wiring     │
  │  ┌──────────────────────────────────────────┐  │
  │  │ Infrastructure   EF Core · DbContext     │  │
  │  │  ┌────────────────────────────────────┐  │  │
  │  │  │ Application   use cases · ports    │  │  │
  │  │  │   ┌──────────────────────────────┐ │  │  │
  │  │  │   │ Domain   entities·invariants │ │  │  │
  │  │  │   └──────────────────────────────┘ │  │  │
  │  │  └────────────────────────────────────┘  │  │
  │  └──────────────────────────────────────────┘  │
  └────────────────────────────────────────────────┘
      dependencies point inward, never outward
```

| Layer | May reference | Must never reference |
|---|---|---|
| **Domain** | nothing at all | EF Core, ASP.NET Core, any NuGet package |
| **Application** | Domain, EF Core (for `DbSet`), FluentValidation | ASP.NET Core, the concrete DbContext |
| **Infrastructure** | Application, Domain, EF Core, Aspire | ASP.NET Core routing |
| **Api** | Application, Infrastructure | EF Core — persistence arrives via Infrastructure |

This is enforced by the compiler, not by convention. Domain has zero package references, so it *cannot* reach for EF Core. Api has no EF Core reference, so it *cannot* touch a DbContext.

Consequences worth knowing:

- **Use cases return `Result<T>`, never HTTP types.** `Error` carries an `ErrorType` of `Validation`, `NotFound` or `Conflict`. `ResultExtensions` in the Api layer is the single place that becomes a status code, so the error policy is auditable in one file — and the same handler can be driven by a message consumer in Phase 3 without modification.
- **CQRS without MediatR**, which is now commercially licensed: three hand-written interfaces (`ICommandHandler<T>`, `ICommandHandler<T,R>`, `IQueryHandler<T,R>`) with explicit DI registration.
- **Persistence is reached through a port.** Application depends on `ICatalogDbContext`; Infrastructure implements it. [ADR 0004](docs/adr/0004-clean-architecture-per-service.md) explains why this rather than repositories.
- **Mapping lives in Infrastructure.** Domain entities carry no EF attributes and are usable with no database present.
- **Migrations are generated with Infrastructure as its own startup project**, which is what keeps EF Core out of the Api layer.

Honest note on cost: for a CRUD service like Catalog these four layers are largely ceremony. The structure earns its keep in Ordering, where the saga, invariants and compensation logic live. It is applied uniformly because one consistent template across six services is worth more than optimising each one.

---

### The user interface

`src/Web/shoplite-web` — Angular 22 with Bootstrap 5, served by the Vite dev server that Aspire starts alongside the services. It talks only to the gateway, never to a service directly.

| Screen | Route | Who sees it |
|---|---|---|
| Sign in / Register | `/login`, `/register` | anyone |
| Products | `/products` | any signed-in user |
| New / Edit product | `/products/new`, `/products/:id/edit` | Admin, Manager |
| Basket | `/basket` | any signed-in user |
| Orders | `/orders` | any signed-in user (Admin sees all) |
| Users and roles | `/users` | Admin |
| Forbidden | `/forbidden` | anyone refused by a guard |

Two rules keep it honest:

- **Every service feature owes a screen.** A capability reachable only by `curl` is not finished. This is why Phase 2 is not complete until the basket is usable in a browser.
- **The UI never hides a degraded response.** A price served from Basket's fallback cache is badged on the line and flagged in the navbar; a 503 from an unreachable Catalog says so instead of showing a generic failure. The alternative — a silently stale price — is worse than an error.

The UI is also the clearest statement of what the access-control model is *not*: the navigation and route guards are convenience, and the API check is the only thing that is security. See §10.

---

## 6. Synchronous plane (HTTP)

```
  Client
    │
    ▼
  Gateway ──────┬──────────────┬──────────────┐
                │              │              │
                ▼              ▼              ▼
            Catalog.Api    Basket.Api    Ordering.Api
                ▲              │
                └──────────────┘
                 price lookup
             (resilience pipeline:
              timeout · retry · circuit breaker)
```

Synchronous calls are confined to request/response paths where the caller cannot proceed without an answer. Exactly one service-to-service HTTP dependency exists — Basket reading prices from Catalog — and it is guarded by a resilience pipeline with a cached fallback, so Catalog being unavailable degrades basket pricing rather than failing checkout.

The fulfilment pipeline contains **no synchronous service-to-service calls**. Adding one would reintroduce the deployment and availability coupling the architecture exists to avoid.

### How that one dependency is wired

Implemented in Phase 2 ([ADR 0006](docs/adr/0006-basket-service-and-synchronous-coupling.md)):

| Concern | Choice |
|---|---|
| Address | `https+http://catalog`, resolved by service discovery — never a port number |
| Contract | `ICatalogPriceReader` port in `Basket.Application`; HTTP lives in Infrastructure |
| Pipeline | 2s per attempt · 2 retries at 200ms · breaker opens at 50% failure over 10s, for 15s |
| Pipeline count | **One.** ServiceDefaults' process-wide handler is removed first, or 3 retries nest inside 3 and one blip becomes nine calls |
| Fallback | Last-known price from Redis (24h TTL), flagged `priceFromCache` so staleness is visible |
| No fallback available | 503, rather than guessing a price |
| Auth | The caller's bearer token is relayed; client credentials is the production answer |

Measured with Catalog stopped: first add **6.45s** (attempts timing out), every add after **~0.02s** once the circuit opened, 503 in 0.03s for an uncached product, and basket reads unaffected throughout.

---

## 7. Asynchronous plane (messaging)

```
   Basket.Api
       │ BasketCheckedOut (event)
       ▼
   ┌─────────────────────────────────────────────┐
   │              Azure Service Bus              │
   └─────────────────────────────────────────────┘
       │                │               │
       ▼                ▼               ▼
   Ordering.Api    Inventory.Api    Payment.Api
   (saga owner)         ▲               ▲
       │                │               │
       └── ReserveStock ┘               │
       └── ProcessPayment ──────────────┘
       │
       │ OrderCompleted / OrderCancelled (events)
       ▼
   Notification.Worker
```

### Message taxonomy

| | Commands | Events |
|---|---|---|
| Intent | imperative — "do this" | factual — "this happened" |
| Recipients | exactly one | zero or more |
| Naming | `ReserveStock` | `StockReserved` |
| Transport entity | queue | topic + subscription |
| Coupling | sender knows the handler | publisher knows nothing of subscribers |

Topics are why Service Bus **Standard** tier is the minimum: Basic has no topic support, and therefore no publish/subscribe.

### Message catalog

| Message | Kind | Published by | Consumed by | Status |
|---|---|---|---|---|
| `BasketCheckedOut` | event | Basket | Ordering | **built** |
| `ReserveStock` | command | Ordering | Inventory | Phase 4 |
| `StockReserved` | event | Inventory | Ordering | Phase 4 |
| `StockUnavailable` | event | Inventory | Ordering | Phase 4 |
| `ProcessPayment` | command | Ordering | Payment | Phase 4 |
| `PaymentSucceeded` | event | Payment | Ordering | Phase 4 |
| `PaymentFailed` | event | Payment | Ordering | Phase 4 |
| `ReleaseStock` | command | Ordering | Inventory | Phase 4 |
| `OrderCompleted` | event | Ordering | Inventory, Notification | Phase 4 |
| `OrderCancelled` | event | Ordering | Notification | Phase 4 |

### Contracts

All of the above live in `ShopLite.Contracts`, the **only** project shared between services. It contains message records and nothing else — no domain logic, no EF Core types, no behaviour. A shared library containing behaviour is how a microservice system quietly becomes a distributed monolith.

Events are versioned additively: new optional fields only. Breaking a contract requires publishing a new message type and retiring the old one once consumers have migrated.

---

## 8. Data architecture

### Database per service

| Database | Owner |
|---|---|
| `ShopLite_Catalog` | Catalog.Api |
| `ShopLite_Identity` | Identity.Api |
| `ShopLite_Ordering` | Ordering.Api |
| `ShopLite_Inventory` | Inventory.Api |
| `ShopLite_Payment` | Payment.Api |

Locally these are databases on one SQL Server 2022 instance; in Azure they are Azure SQL databases on one logical server. **Separate databases on a shared instance is a legitimate implementation of the pattern** — what makes it real is the rule, not the physical separation:

- One `DbContext` per service, one migration history per database
- No cross-database queries, no three-part names
- No service holds a connection string for another service's store

Basket is the exception that proves the rule rather than breaking it: its store is Redis, not a database on this instance, and the same constraint applies — nobody else connects to it. Its fallback price cache lives there too, so "no shared store" survives the addition of a cache.

Cross-database queries are impossible in Azure SQL, so the deployment target enforces this independently of developer discipline.

### Transactional outbox

Each service's outbox lives **inside that service's own store** — note *store*, not *table*: Basket's is a Redis list, because Basket's state is in Redis. ([ADR 0008](docs/adr/0008-asynchronous-messaging.md)) This is a correctness requirement, not a convention: the outbox exists so that a state change and the publication of its event commit in a single transaction. Placing the outbox in a separate or shared database reinstates the dual-write problem it was introduced to solve.

```
  BEGIN TRANSACTION
     UPDATE  stock levels
     INSERT  outbox row (StockReserved)
  COMMIT
     ─────► background publisher reads outbox ─────► Service Bus
```

A crash before commit leaves no state change and no event. A crash after commit leaves both, and the publisher retries. There is no window in which one exists without the other.

### Idempotency

Because delivery is at-least-once, every consumer must tolerate redelivery. Consumers deduplicate on message ID, and commands with external side effects (notably `ProcessPayment`) carry an idempotency key that the handler checks before acting.

---

## 9. The fulfilment saga

Ordering owns the process. A saga — not a chain of services each calling the next — because orchestration keeps the sequence, its failure handling, and its state in one readable place.

```
                      BasketCheckedOut
                             │
                             ▼
                     ┌───────────────┐
                     │   Submitted   │
                     └───────┬───────┘
                             │  ──► ReserveStock
                  ┌──────────┴───────────┐
            StockReserved         StockUnavailable
                  │                      │
                  ▼                      ▼
          ┌───────────────┐       ┌─────────────┐
          │ StockReserved │       │  Cancelled  │
          └───────┬───────┘       └─────────────┘
                  │  ──► ProcessPayment
         ┌────────┴─────────┐
   PaymentSucceeded    PaymentFailed
         │                  │
         ▼                  ▼
   ┌───────────┐    ┌──────────────────┐
   │   Paid    │    │   Compensating   │
   └─────┬─────┘    │  ──► ReleaseStock│
         │          └────────┬─────────┘
         ▼                   ▼
   ┌───────────┐       ┌─────────────┐
   │ Completed │       │  Cancelled  │
   └───────────┘       └─────────────┘
```

Saga state is persisted to `ShopLite_Ordering`, so an Ordering restart mid-flow resumes rather than losing the order.

### Compensation, not rollback

There is no mechanism to roll back a committed transaction in another service. Inventory has already written its reservation. The saga therefore issues `ReleaseStock` — a new forward operation whose effect is to undo the previous one. Compensating actions are ordinary commands and can themselves fail and be retried.

### The hard case

Payment crashes *after* charging the card but *before* publishing `PaymentSucceeded`. The charge happened; no event exists. This is handled by making payment idempotent on its idempotency key: the saga's retry reaches Payment again, Payment recognises the key, does not charge twice, and publishes the result it already recorded.

This case is the reason the architecture looks the way it does. Any design that cannot answer it is incomplete.

### Timeouts

A reservation that is never confirmed must not be held forever. The saga schedules a timeout when entering `StockReserved`; if payment has not resolved by then, it compensates and cancels. Scheduled messages are a broker feature, not a background polling loop.

---

## 10. Cross-cutting concerns

| Concern | Approach |
|---|---|
| **Identity** | `Identity.Api` using ASP.NET Core Identity, issuing JWTs with roles as claims. Services validate the signature themselves and never call Identity per request. Identity propagates across messages as a contract field, not an ambient context. Validation and the role policies live once in `ShopLite.ServiceDefaults`. See [ADR 0005](docs/adr/0005-identity-and-angular-ui.md), and §16 for the Keycloak migration path. |
| **Observability** | OpenTelemetry traces, metrics and logs over OTLP. A single trace spans gateway → service → broker → consumer. Aspire dashboard locally, Application Insights in Azure. |
| **Correlation** | Correlation IDs survive queue hops via message headers. |
| **Resilience** | `Microsoft.Extensions.Http.Resilience` for HTTP; Wolverine error policies and dead-letter queues for messaging. |
| **Health** | `/alive` (liveness — is the process up) and `/health` (readiness — are dependencies reachable) are separate endpoints with separate meanings. |
| **Configuration** | `ServiceDefaults` applies telemetry, health, discovery and resilience uniformly. User secrets locally; Key Vault with Managed Identity in Azure. |
| **Service discovery** | Logical names (`https://catalog`) resolved by Aspire locally and by Container Apps in Azure. No hardcoded ports. |

---

## 11. Deployment architecture

One AppHost definition, two targets. Aspire resources declare where they run:

```csharp
var sql = builder.AddAzureSqlServer("sql").RunAsContainer();
var sb  = builder.AddAzureServiceBus("messaging").RunAsEmulator();
```

| Concern | Local (`aspire run`) | Azure (`azd up`) |
|---|---|---|
| Compute | local processes | Azure Container Apps |
| Relational data | local SQL Server 2022 instance | Azure SQL Database |
| Messaging | **RabbitMQ** container (the Service Bus emulator does not work — ADR 0008) | Azure Service Bus, Standard |
| Cache | Redis container | Redis container app |
| Identity | `Identity.Api` + local SQL Server | `Identity.Api` on Container Apps + Azure SQL |
| Web UI | Angular dev server (Vite) | static files on Container Apps |
| Images | local Docker | Azure Container Registry |
| Secrets | user secrets | Key Vault + Managed Identity |
| Telemetry | Aspire dashboard | Application Insights |
| Scaling | n/a | KEDA rules on queue depth; scale to zero |

Authentication to Azure resources uses **Managed Identity**. No connection strings or passwords are stored in configuration or source. Locally the equivalent is Windows integrated authentication — also passwordless, so the pattern is consistent across both environments.

Phases 1–6 run entirely locally; Azure deployment is Phase 7. Development does not target cloud resources.

---

## 12. Key decisions and trade-offs

| Decision | Rationale | Cost accepted |
|---|---|---|
| Saga orchestration over choreography | Sequence and failure handling are readable in one place | Ordering becomes a coordination hub |
| Azure Service Bus over RabbitMQ, for deployment | Managed service, Managed Identity, KEDA, portal tracing | ~$10/month; and its emulator proved unusable, so local development runs RabbitMQ instead — what is tested is not quite what ships ([ADR 0008](docs/adr/0008-asynchronous-messaging.md)) |
| Wolverine over MassTransit | MassTransit v9 is commercially licensed; v8 is in maintenance | Smaller community and fewer tutorials |
| No MediatR / AutoMapper | Both moved to commercial licensing | Hand-written mapping and dispatch |
| Database per service on a shared instance | Preserves the rule at realistic cost | Isolation is enforced by convention locally, by Azure SQL in deployment |
| Redis for Basket | Basket is ephemeral key-value data with a natural TTL, and the TTL is one argument to a set rather than a scheduled sweep ([ADR 0007](docs/adr/0007-basket-on-redis.md)) | A second storage technology to operate, and Docker becomes required to run the project |
| Fallback price cache in Basket's own store | A circuit breaker fails fast, not softly — something has to answer when Catalog is down | Prices are stale by up to the 24h TTL; staleness is surfaced on the response |
| `Idempotency-Key` on add-item only | Add increments, so a retry double-counts; set-quantity assigns, so it cannot | One operation has a dedupe mechanism and its neighbour does not, which looks inconsistent until you read why |
| Token relay from Basket to Catalog | No new infrastructure, and the caller's identity is preserved end to end | Basket can do anything its caller can; client credentials is the production answer |
| ASP.NET Core Identity over Keycloak | A schema we own and can inspect; no extra container; shows how a JWT is minted and validated | No SSO, social login, MFA or password reset without building them (§16) |
| Roles as JWT claims, not a lookup | No network hop per request; Catalog survives Identity being down | A role change applies only at next sign-in |
| Symmetric JWT signing | One shared key, no key distribution to set up | Every service holds a key that can also mint tokens — wrong for production |
| Angular + Bootstrap UI, treated as required | Makes role-based access and degraded responses visible instead of theoretical | Frontend surface area unrelated to distribution, and every new service feature now owes a screen |

---

## 13. Known limitations

- **The Service Bus emulator is unsupported and incomplete** (no Entra ID, no virtual networks, no partitioned entities, no UI). Local behaviour diverges from Azure at the edges; first deployment is expected to surface at least one such divergence.
- **Azure SQL's free tier is serverless with auto-pause**, so the first request after idle incurs a cold start.
- **Payment is simulated.** No real payment provider is integrated; failure modes are injected deliberately.
- **Local SQL Server is Developer Edition**, which exposes Enterprise features Azure SQL lacks. Features unavailable in Azure SQL are not used.
- **Single-region, single-writer.** No geo-replication, multi-region failover, or read replicas.
- **No event sourcing.** CQRS read models are projected from events; the write model is conventional state.

---

## 14. Anti-patterns explicitly avoided

- A shared database between services
- A shared library containing domain logic or behaviour
- Synchronous call chains in the fulfilment path
- Entity services (CRUD wrappers over a table, mistaken for bounded contexts)
- Services reading each other's tables
- Observability deferred to "later"
- Distributed transactions

---

## 15. When this architecture would be the wrong choice

For a greenfield product with an unproven domain and a small team, a **modular monolith** with clean internal boundaries would be the correct starting point. It delivers faster, is far cheaper to operate, and allows boundaries to be moved once the domain is understood — which is precisely when extracting services becomes cheap and low-risk.

ShopLite is distributed deliberately, to surface the costs and failure modes of distribution. Recognising when those costs are not worth paying is part of the competence this project is meant to demonstrate.

---

## 16. Migrating to Keycloak later

Keycloak was the original choice, replaced by `Identity.Api` in [ADR 0005](docs/adr/0005-identity-and-angular-ui.md). The swap remains available, and the current design was kept deliberately compatible with it.

### Why it is a contained change

JWT validation and the role policies live in **one file** — `ShopLite.ServiceDefaults/AuthenticationExtensions.cs`. Endpoints reference a policy name, never a role list. Services already validate the token signature themselves rather than calling the issuer. Keycloak issues standard OIDC JWTs, so from a service's point of view only *who signed the token* changes.

### What does not change at all

- `.RequireAuthorization(Policies.ManageCatalog)` on every endpoint
- The `Microsoft.AspNetCore.Authentication.JwtBearer` package and middleware order
- The Angular route-guard and interceptor structure
- Services never calling the identity provider per request

### What does change

| Area | Now | With Keycloak |
|---|---|---|
| Token issuer | `Identity.Api` / `TokenService` | Keycloak; our token code is deleted |
| Signing | symmetric HMAC, shared secret | asymmetric RS256, public keys fetched from JWKS |
| Validation config | `IssuerSigningKey` with a secret | `options.Authority` — keys are discovered automatically |
| Login flow | SPA posts email + password to our API | SPA **redirects** to Keycloak's hosted login page (Authorization Code + PKCE) |
| Login UI | our Angular form | Keycloak's page, themeable |
| User admin | our Users screen | Keycloak admin console, or its Admin REST API |
| Sessions | one access token, no refresh | access + refresh tokens, silent renewal |
| Hosting | a .NET project | a container (`AddKeycloak` in `Aspire.Hosting.Keycloak`) |

Note that validation gets *simpler*: `Authority` replaces manual key handling, and no service holds a token-minting secret any more.

### The gotchas, in the order they will bite

1. **Role claims are in the wrong place.** Keycloak emits roles nested under `realm_access.roles`, not as the flat `ClaimTypes.Role` entries that `RequireRole` reads. Authorization silently denies everything until a claims transformation flattens them. This is the single most common sticking point; budget an evening for it alone.
2. **The audience claim.** Keycloak's default `aud` is `account`, not your API. Validation fails until a dedicated audience mapper or client scope is configured, or audience validation is explicitly relaxed.
3. **The login flow is a redirect, not a form post.** This is the real work on the frontend. The SPA leaves the page, comes back with a code, and exchanges it for tokens. It needs an OIDC library (`angular-oauth2-oidc`), and our login and register components are deleted rather than adapted.
4. **Realm configuration must be code.** Clicking through the admin console produces a setup nobody can reproduce. Export the realm to JSON and import it on container start, or the environment is unrepeatable.
5. **Realm roles vs client roles vs groups.** Keycloak offers three overlapping models. Pick one — realm roles map most directly onto what exists today — and write down why.
6. **Issuer URL must match exactly.** The `iss` in the token and the configured authority must agree character for character, including scheme, port and trailing path. Container-internal versus browser-visible hostnames differ, and this breaks far more often than it should.
7. **Existing users.** The three seeded accounts are trivial to recreate; real users need importing through the Admin API.
8. **Refresh tokens change the frontend's shape.** Short access tokens plus silent renewal replace "one token in localStorage until it expires".

### What to avoid doing now, so the door stays open

- Don't scatter role string literals through endpoints or templates — keep using `Roles` and `Policies`.
- Don't let components read token internals; everything goes through `AuthService`.
- Don't add anything to `ShopLite_Identity` beyond users and roles. Profile data that other services need belongs to those services, or the identity provider becomes impossible to replace.
