# 7. Basket on Redis

**Date:** 2026-11-07
**Status:** Accepted
**Supersedes:** the "Basket on SQL Server, not Redis" decision in [ADR 0006](0006-basket-service-and-synchronous-coupling.md). Restores the original intent recorded in ARCHITECTURE.md §12.

## Context

ADR 0006 put Basket on SQL Server for one reason only: Redis needs a container and Docker Desktop was not running. Docker is now running, so the reason is gone.

## Decision

Basket stores baskets, fallback prices and idempotency markers in **Redis**, run as an Aspire-managed container. `ShopLite_Basket` has been dropped; `Basket.Infrastructure` no longer references EF Core.

This is the first service whose store Aspire **owns**: it pulls the image, starts the container, injects the connection string, and stops it on shutdown. Catalog and Identity reference a SQL Server that exists independently.

### Key layout

Redis has no schema, so the key layout *is* the schema. It lives in one file, `RedisKeys`.

| Key | Holds | TTL |
|---|---|---|
| `basket:{customerId}` | the whole basket as one JSON document | 30 days, refreshed on write |
| `price:{productId}` | last price seen from Catalog, for the fallback | 24 hours |
| `idem:{requestId}` | an applied `Idempotency-Key` | 24 hours |

One key per basket rather than a hash per line: every operation reads the basket, applies a rule and writes it back, so splitting it would buy partial updates nobody asks for and give up the guarantee that a basket is never half-written.

### What this fixed

Two weaknesses ADR 0006 recorded as known limitations are now gone, not worked around:

- **Unbounded price staleness.** The SQL cache had no expiry, so the fallback could serve an arbitrarily old price. A TTL bounds it, and an unconfirmed price now simply stops existing.
- **An idempotency table that grows forever.** ADR 0006 planned a background sweep. `idem:` keys expire on their own.

A third improvement was unplanned: the write-through cache no longer has to defer its save to the caller's `SaveChanges`, which removes an ordering coupling that was easy to break.

### Atomicity without constraints

The SQL version got idempotency from a primary key: the insert failed and EF surfaced it. Redis has no constraints, so `Save` uses `MULTI`/`EXEC` with a server-side `KeyNotExists` condition. If the request id appeared since the handler's pre-check, `EXEC` does not run and neither the basket nor the marker is written.

Same guarantee, different mechanism — and the reason the request id is a parameter of `Save` rather than a separate `MarkRequestApplied` call. The transaction boundary is a property of the store, so the port expresses it instead of hoping callers remember the ordering.

### A repository, not a DbContext port

Catalog's Application layer takes `ICatalogDbContext` and writes queries against `DbSet` — the compromise in [ADR 0004](0004-clean-architecture-per-service.md). Basket takes `IBasketRepository`, which deals in whole aggregates, because a key-value store has nothing to query.

The side effect is worth noticing: **`Basket.Application` now has no persistence dependency at all.** Not because anyone tried harder than in Catalog, but because the store does not tempt the layer into queries.

### Rehydration on the aggregate

With no ORM there is no change tracker to materialise entities through private setters, so `CustomerBasket.Rehydrate` and `BasketItem.Rehydrate` exist. Named factories rather than public setters, so the one legitimate caller is obvious, and `BasketRecord`/`BasketItemRecord` sit between the domain and the JSON so that renaming a domain field cannot silently make every stored basket unreadable.

`BasketItemRecord` omits the basket id: in a document store it is the key of the document the line already sits inside, and a second copy of the same fact is free to drift.

### Secure by default, as it happens

Aspire starts Redis with `--requirepass` and a generated password, TLS on 6379, and `--save 60 1`. Worth knowing when reaching for `redis-cli`: the plaintext port is 6380 and it needs `-a`.

## Consequences

- **Docker is now required to run the project**, which it was not before. It becomes required in Phase 3 regardless.
- Two SQL databases, not three: `ShopLite_Catalog` and `ShopLite_Identity`.
- Polyglot persistence is real rather than described — and the basket is now inspectable in RedisInsight, which was part of the original argument for choosing SQL Server.
- No migrations for Basket. One fewer step, and one fewer safety net: nothing versions the stored document shape, so `BasketRecord` has to stay able to read what is already in Redis.
- `WithDataVolume()` keeps baskets across restarts. Aspire removes the container on shutdown but the named volume survives, so a fresh container picks the data back up.

## Known limitation, carried over and now clearer

Adding more of a product already in the basket still calls Catalog, even though the line already holds a price snapshot and the increment needs no price. With Catalog down and nothing cached, that returns 503 for an operation that could have succeeded from state Basket already had.

Defensible as it stands — the call confirms the product still exists and refreshes the fallback cache — but it is availability given away for a check nobody asked for. Skipping the price lookup when the line exists would be a small change to `AddItemHandler` and is the better behaviour.

Related and visible in testing: `priceFromCache` describes **where that line's snapshot came from**, so re-adding to an existing line keeps `false` even while Catalog is down. That is correct, and it means the flag is not a live indicator of Catalog's health.
