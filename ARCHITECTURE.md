# ShopLite — Architecture

**Status:** target architecture. Implementation is in progress; build sequence and progress are tracked in [ROADMAP.md](ROADMAP.md).
**Last updated:** 2026-11-04

---

## 1. Overview

ShopLite is a microservices implementation of an e-commerce **checkout and order-fulfilment pipeline**. It deliberately covers one narrow slice of a store — the path from a populated basket to a paid, confirmed order — rather than a broad feature set.

That slice is the point. It crosses five service boundaries, has to remain correct when any participant fails midway, and therefore exercises the problems that distinguish distributed systems from a monolith with more projects in it: eventual consistency, the dual-write problem, idempotent delivery, and distributed rollback via compensation.

Out of scope by design: product search, reviews, recommendations, shipping carriers, returns, and any user interface.

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
                       │ (.http / Scalar) │
                       └────────┬─────────┘
                                │ HTTPS
                       ┌────────▼─────────┐        ┌──────────────┐
                       │  ShopLite.Gateway │───────►│   Keycloak   │
                       │      (YARP)       │  OIDC  │  (identity)  │
                       └────────┬─────────┘        └──────────────┘
                                │
                    ┌───────────┴────────────┐
                    │     ShopLite services  │
                    │  (see §5 and §6, §7)   │
                    └────────────────────────┘
```

The gateway is the only publicly reachable component. No service is exposed directly.

---

## 5. Service catalog

| Service | Responsibility | Data store | Public API |
|---|---|---|---|
| **ShopLite.Gateway** | Single entry point: routing, JWT validation, rate limiting | none | yes |
| **Catalog.Api** | Product information and pricing | `ShopLite_Catalog` | via gateway |
| **Basket.Api** | Per-customer basket; initiates checkout | Redis | via gateway |
| **Ordering.Api** | Order lifecycle; owns the fulfilment saga | `ShopLite_Ordering` | via gateway |
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

| Message | Kind | Published by | Consumed by |
|---|---|---|---|
| `BasketCheckedOut` | event | Basket | Ordering |
| `ReserveStock` | command | Ordering | Inventory |
| `StockReserved` | event | Inventory | Ordering |
| `StockUnavailable` | event | Inventory | Ordering |
| `ProcessPayment` | command | Ordering | Payment |
| `PaymentSucceeded` | event | Payment | Ordering |
| `PaymentFailed` | event | Payment | Ordering |
| `ReleaseStock` | command | Ordering | Inventory |
| `OrderCompleted` | event | Ordering | Inventory, Notification |
| `OrderCancelled` | event | Ordering | Notification |

### Contracts

All of the above live in `ShopLite.Contracts`, the **only** project shared between services. It contains message records and nothing else — no domain logic, no EF Core types, no behaviour. A shared library containing behaviour is how a microservice system quietly becomes a distributed monolith.

Events are versioned additively: new optional fields only. Breaking a contract requires publishing a new message type and retiring the old one once consumers have migrated.

---

## 8. Data architecture

### Database per service

| Database | Owner |
|---|---|
| `ShopLite_Catalog` | Catalog.Api |
| `ShopLite_Ordering` | Ordering.Api |
| `ShopLite_Inventory` | Inventory.Api |
| `ShopLite_Payment` | Payment.Api |

Locally these are four databases on one SQL Server 2022 instance; in Azure they are four Azure SQL databases on one logical server. **Separate databases on a shared instance is a legitimate implementation of the pattern** — what makes it real is the rule, not the physical separation:

- One `DbContext` per service, one migration history per database
- No cross-database queries, no three-part names
- No service holds a connection string for another service's database

Cross-database queries are impossible in Azure SQL, so the deployment target enforces this independently of developer discipline.

### Transactional outbox

Each service's outbox tables live **inside that service's own database**. This is a correctness requirement, not a convention: the outbox exists so that a state change and the publication of its event commit in a single transaction. Placing the outbox in a separate or shared database reinstates the dual-write problem it was introduced to solve.

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
| **Identity** | Keycloak (OIDC). Gateway validates JWTs; services re-validate rather than trusting the gateway. Identity propagates across messages as a contract field, not an ambient context. |
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
| Messaging | Service Bus emulator container | Azure Service Bus, Standard |
| Cache | Redis container | Redis container app |
| Identity | Keycloak container | Keycloak container app |
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
| Azure Service Bus over RabbitMQ | Managed service, Managed Identity, KEDA, portal tracing | ~$10/month; local emulator is unsupported and incomplete |
| Wolverine over MassTransit | MassTransit v9 is commercially licensed; v8 is in maintenance | Smaller community and fewer tutorials |
| No MediatR / AutoMapper | Both moved to commercial licensing | Hand-written mapping and dispatch |
| Database per service on a shared instance | Preserves the rule at realistic cost | Isolation is enforced by convention locally, by Azure SQL in deployment |
| Redis for Basket | Basket is ephemeral key-value data with a natural TTL | A second storage technology to operate |
| Keycloak over Entra External ID | Runs locally, free, standard OIDC | Not an Azure-native identity solution |
| No UI | Adds surface area without teaching distribution | Demonstrated via Scalar and `.http` files |

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
