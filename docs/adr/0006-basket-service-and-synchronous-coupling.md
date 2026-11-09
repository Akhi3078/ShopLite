# 6. Basket service and synchronous coupling

**Date:** 2026-11-07
**Status:** Partly superseded by [ADR 0007](0007-basket-on-redis.md), which moves Basket to Redis once Docker was available. Everything here about the Catalog dependency, the resilience pipeline and idempotency still stands; only the storage decision changed.
**Amended:** the "Redis for Basket" row in ARCHITECTURE.md §12 — since restored.

## Context

Phase 2 of [ROADMAP.md](../../ROADMAP.md) asks for a second service with its own database, a typed `HttpClient` between the two, a resilience pipeline, and a demonstration that retry without idempotency is a bug. Basket is the natural second service: it is the only one in the design that genuinely needs an answer from another service before it can respond.

## Decisions

### Basket on SQL Server, not Redis — *superseded by [ADR 0007](0007-basket-on-redis.md)*

ARCHITECTURE.md §12 chose Redis for Basket, on the reasoning that a basket is ephemeral key-value data with a natural TTL. That reasoning still holds. Basket is nonetheless on SQL Server (`ShopLite_Basket`) for now, because Redis in this setup means an Aspire-managed container and Docker Desktop is not running on this machine.

What this costs: the polyglot-persistence lesson, and TTL-based expiry, which would now need a scheduled sweep instead.

What it buys: Phase 2 proceeds without a Docker dependency, the EF Core patterns stay identical to Catalog's, and the fallback price cache gets somewhere durable to live. Docker becomes a hard requirement in Phase 3 for the Service Bus emulator, which is the natural moment to revisit this; the change is contained to `Basket.Infrastructure`, since no Domain or Application type knows which store is underneath.

### Four projects, as in Catalog

Basket follows [ADR 0004](0004-clean-architecture-per-service.md) rather than Identity's single-project shape. Unlike Identity, Basket has real rules of its own — one line per product, a quantity ceiling, a price snapshot that must not be silently refreshed — so there is something for a Domain layer to protect.

### The Catalog dependency is a port, not an HttpClient

`ICatalogPriceReader` lives in `Basket.Application`; `CatalogHttpPriceReader` lives in `Basket.Infrastructure`. Handlers see three outcomes — a price, `NotFound`, or `Unavailable` — and never learn that HTTP, retries, or a cache were involved.

This is what makes the fallback testable and the handler readable. It also means swapping the synchronous call for a Catalog-published price event in Phase 3 changes one class.

### Service discovery, not a port number

The client's base address is `https+http://catalog`, resolved at request time from configuration the AppHost injects via `WithReference(catalog)`. Hardcoding `https://localhost:7232` would work today and break the moment Catalog moves, scales to more than one replica, or runs in a container.

### One resilience pipeline, not two

`ShopLite.ServiceDefaults` already adds `AddStandardResilienceHandler()` to every `HttpClient` in the process. Basket's Catalog client calls `RemoveAllResilienceHandlers()` first and then adds its own tuned pipeline.

Without the removal, the two pipelines nest: 3 retries inside 3 retries is 9 calls to a service that is already struggling. Retry amplification is the mechanism by which a brief blip becomes an outage, and it is easy to build accidentally precisely because each layer looks reasonable on its own.

The tuned values — 2s per attempt, 2 fast retries, a breaker that opens at a 50% failure ratio over 10s and stays open 15s — are chosen to be observable while learning rather than to be optimal.

`RemoveAllResilienceHandlers` is marked experimental (`EXTEXP0001`) and the diagnostic is suppressed at the call site. The alternative, reaching into the named options of a handler registered in another project, is quieter but breaks silently if ServiceDefaults changes.

### A fallback cache in Basket's own database

Every successful price read is written through to a `CachedProductPrices` table. When Catalog is unreachable, the reader answers from that table and flags the price `FromCache`, which reaches the HTTP response as `priceFromCache` on the line and `containsCachedPrices` on the basket.

Three things this gets right, and one it does not:

- A circuit breaker **fails fast, not softly**. It stops hammering a dead service; it does not keep your feature working. The cache is what keeps the feature working.
- Degradation is **visible**. A stale price presented as current is a worse outcome than an error, so the staleness travels with the data.
- With no cached entry, the request **fails** with 503 rather than guessing a price.
- The cache has no expiry. A price could be arbitrarily old, and right now nothing says how old. *Fixed by [ADR 0007](0007-basket-on-redis.md): Redis gives it a 24-hour TTL.*

The table lives in Basket's own store, not a shared one, so the "no shared store" rule survives the addition.

### Idempotency on add-item, by primary key

`POST /basket/items` accepts an optional `Idempotency-Key` header, recorded in a `ProcessedRequests` table whose **primary key is the key itself**. The item and the marker commit in one `SaveChanges`, so there is no window in which a crash makes an applied operation replayable.

This is the concrete answer to "why is retry without idempotency a bug":

- Adding an item **increments** a quantity. Replaying it adds the quantity twice.
- A retry is not hypothetical. A resilience pipeline, an impatient second click, or a gateway that timed out after the work committed all produce one.
- Retrying a `GET` is free; retrying a `POST` is a business decision. The standard resilience handler does not know the difference, which is why the safety has to be built at the operation, not the transport.

Note the contrast with `PUT /basket/items/{productId}`, which **assigns** a quantity. It is naturally idempotent and needs no key at all. Where a safe operation is available, choosing it beats bolting a dedupe table onto an unsafe one — a lesson that returns in Phase 3, where every message consumer needs it.

### Token relay to Catalog, for now

Catalog requires authentication, so Basket forwards the caller's bearer token. This preserves the caller's identity end to end and needs no new infrastructure, but it means Basket can do anything its caller can.

Correct for production: Basket authenticates as itself via the client-credentials flow, with its own scopes. Deferred because it needs an identity provider that issues service tokens, which arrives with the Keycloak migration in ARCHITECTURE.md §16. Recorded here so the shortcut is known rather than discovered.

### `ICallerContext` instead of `IHttpContextAccessor`

The customer id comes from the token's `sub` claim and the bearer token from the request header, both read in `Basket.Api` — the only project with an HttpContext. Injecting `IHttpContextAccessor` into the HTTP client would have dragged a `FrameworkReference` into two class libraries that have no business with one.

A consequence worth stating: there is no `/baskets/{customerId}` route. The basket is always the caller's, so there is no authorization check to forget.

### The gateway does not strip Basket's prefix

Identity and Catalog expose their resources at the root (`/auth`, `/products`), so the gateway's `/identity` and `/catalog` prefixes are routing labels that must be removed. Basket's own resource is already `/basket`, so prefix and service path coincide and the request forwards unchanged.

The cost is that Basket's Scalar page is not reachable through the gateway. Accepted rather than renaming the resource to make a routing detail tidier.

## Consequences

- A third store: `ShopLite_Catalog`, `ShopLite_Identity`, and Basket's own (Redis, per [ADR 0007](0007-basket-on-redis.md)).
- The first real failure mode in the system: Basket's behaviour now depends on Catalog's availability. It degrades rather than failing, but it is no longer independent.
- Price snapshots mean a basket can disagree with the catalog. That is intended — the customer is owed the price they were shown — and it is the same trade-off, in miniature, that Phase 3 makes with eventual consistency.
- `ErrorType.Unavailable` exists in Basket's `Error` and not in Catalog's, because Catalog calls no one.
- Still no automated tests. The verification in this phase was manual, and the resilience behaviour in particular deserves a test with a stub server rather than a stopped process.
