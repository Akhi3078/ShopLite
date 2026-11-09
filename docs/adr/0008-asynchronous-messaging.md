# 8. Asynchronous messaging

**Date:** 2026-11-08
**Status:** Accepted

## Context

Phase 3 of [ROADMAP.md](../../ROADMAP.md) is the phase its own text calls *"where microservices actually start"*. Phases 1–2 built boundaries and made one synchronous call survivable. This one removes the need for the call.

Basket had to produce an order without depending on Ordering being alive, and Ordering had to create one without asking anybody anything.

## Decisions

### Azure Service Bus, run locally as the emulator

`builder.AddAzureServiceBus("messaging").RunAsEmulator()` — one declaration, an emulator container locally and a real namespace when published.

Three things about the emulator that cost real time and are worth writing down:

- **It needs a SQL Server sidecar.** Aspire declares `messaging-mssql` automatically, but it is a 2.35 GB image.
- **It halts itself if that SQL is not ready.** Not retries-forever: `Unrecoverable action : SQL Health Check failed ... halting execution`, then exit 139. On a cold machine the pull outlasts the emulator's patience, so the first run fails and the second succeeds. Pre-pulling the image is the fix.
- **Entities must be declared, not created at runtime.** The emulator applies its configuration at startup and anything a client creates afterwards does not survive a restart.

**Auto-provisioning is on for RabbitMQ and off for Azure Service Bus.** RabbitMQ declares exchanges, queues and bindings over AMQP itself, the operations are idempotent, and Aspire recreates the container each run. Azure Service Bus provisioning goes through a management API the emulator does not serve — enabling it there made every service retry 20 times and never finish starting. Against real Azure, infrastructure belongs in deployment rather than in whichever service starts first.

- The entities declared in the AppHost **never reached the emulator**. No configuration file was mounted into the container, and Wolverine's subscription listener never started as a result.
- The failure was **silent**. `PublishAsync` succeeded, the outbox drained, Ordering's listener had nothing to read, and no error appeared anywhere. Two full checkout cycles completed with no order created and nothing in any log to explain it. Declaring the entities before `RunAsEmulator()` rather than after made no difference.
- Aspire **destroys and recreates the emulator container on every run**, so provisioning that does not survive a restart costs nothing: the services re-provision on their next startup.

Against real Azure this remains the wrong production default — infrastructure should be deployed, not created by whichever service starts first. It is right for the emulator, and the AppHost declarations are kept because they document the intended topology even though the emulator ignores them.

### Topic and subscription, not a queue

`BasketCheckedOut` goes to a **topic** with one subscription per consumer (`ordering-service`).

A queue would have worked today, with one consumer. The topic is what makes adding a second consumer — a notification service, an analytics sink — a change to *that* service's deployment and nothing else. Basket does not learn about it.

Note the naming trap: the subscription cannot be called `ordering`, because Aspire keeps **one flat namespace for all resource types** and the project is already called that. The AppHost refuses to start, with a clear message.

### An event, not a command

`BasketCheckedOut` is past tense and states a fact. Basket does not send `CreateOrder`, because that would mean Basket knows an order should exist — and the next time someone wants checkout to also award loyalty points, Basket would have to know that too.

The event carries **everything Ordering needs**, including product names and prices, not just ids. A thinner message would force Ordering to call back to Basket or Catalog, reintroducing exactly the synchronous coupling this phase exists to remove. The cost is a fatter message and duplicated data. That is the right trade.

### The outbox lives in Basket, and in Redis

ROADMAP build-order item 6 says "Ordering's outbox, hand-rolled first". It is in **Basket** instead, because that is where the dual write actually is in this flow: checkout changes state *and* publishes. Ordering, for now, only consumes — it will need an outbox in Phase 4, when it starts sending commands to Inventory and Payment.

ARCHITECTURE.md §8 shows a SQL outbox table. The principle it states is the one that matters — *the outbox lives in the same store as the state change* — and Basket's store is Redis, so the outbox is a Redis list. Putting Basket's outbox in SQL Server would recreate the dual-write problem it exists to solve.

Mechanics: `MULTI`/`EXEC` writes the event to `outbox:pending` and deletes the basket key atomically; a `BackgroundService` claims entries with `RPOPLPUSH` onto `outbox:inflight`, publishes, then removes. A crash between publish and removal republishes — which is precisely why consumers must be idempotent.

Hand-rolled on purpose, before switching to Wolverine's durable outbox. The library version is one configuration call, and it is not possible to appreciate what that call buys, or debug it when it misbehaves, without having built the thing it replaces.

### Idempotency in two halves

Ordering checks `ProcessedMessages` before doing anything, and relies on its **primary key** to settle races. The marker and the order commit in one `SaveChanges`, so there is no window where one exists without the other.

Both halves are needed. The pre-check handles the common sequential redelivery cheaply; the key handles two deliveries racing. The `DbUpdateException` filter checks *which entity* failed, because catching every persistence error here would acknowledge messages that were never processed — the same mistake that masked a real bug in Phase 2.

A duplicate is reported as **success**, not failure. Throwing would retry, fail identically, and dead-letter — turning working duplicate detection into a false alarm.

### Retryable versus poison, decided in the handler

The message handler distinguishes two failures that look identical to the broker:

- **Validation** — the message itself is wrong, so every retry produces the same result. Thrown immediately to the dead-letter queue.
- **Anything else** — assumed transient, handed back to the retry policy (200ms, 1s, 5s, then dead-letter).

Wolverine cannot make this call; only the handler knows. Getting it wrong in the lenient direction means a poison message retried forever, blocking everything behind it.

### `WolverineFx.RuntimeCompilation` is required

Wolverine 6 moved its runtime compiler out of the core package. Without it every service starts and then dies with *"no IAssemblyGenerator (Roslyn) is registered"* — which reads like a dependency-injection problem and is not.

The production answer is to pre-generate handler code at build time and set `TypeLoadMode.Static`, removing Roslyn from the runtime. Deferred because it adds a build step to every service and nothing here is startup-time sensitive.

### Checkout answers 202, not 201

The basket is emptied and the event recorded, but no order exists yet. `201 Created` would name a resource the caller cannot fetch; `200 OK` would imply the work is done. `202 Accepted` is the honest answer, and it is the first place in this project where eventual consistency is visible from outside.

## Consequences

- **A third service** (`Ordering.Api`, four projects) and a fourth database (`ShopLite_Ordering`).
- **`ShopLite.Contracts`** exists — the only project shared between services, holding message records and nothing else. No package references, deliberately: needing one would be the signal that something has crept in.
- **Eventual consistency is now user-visible.** After checkout the basket is empty and no order exists for a moment. The orders screen says so instead of showing an empty list as though something failed.
- **Orders never leave `AwaitingFulfilment`.** Nothing yet reserves stock or takes payment; that is Phase 4.
- **Docker is required**, and the first run on a clean machine needs patience or a pre-pull.
- Ordering has **no synchronous dependency on anything** — no `WithReference(catalog)`, no `WithReference(basket)`. That absence is the deliverable.

## RabbitMQ locally, Azure Service Bus for deployment

The Azure Service Bus emulator could not run this flow, so local development uses RabbitMQ and Azure Service Bus remains the published target. `builder.ExecutionContext.IsRunMode` picks between them in the AppHost.

**Why the emulator was abandoned.** Two states were available and both were broken:

- **Without a subscription rule**, the subscription matched nothing. Real Azure adds a `$Default` TrueFilter automatically; Aspire's generated config emits `"Rules": []` and the emulator adds none. Publishing succeeded, the outbox drained, the listener sat idle, and nothing errored anywhere.
- **With a rule**, the emulator logged `Processing filter`, then `Triggering Entity Sync`, then died with `Out of memory.` and exit 139 — reproduced three times, with both a default rule and an explicit empty correlation filter, on a Docker daemon with 7.6 GB available and under 1 GB in use.

ARCHITECTURE.md §13 warned that *"the Service Bus emulator is unsupported and incomplete"* and that a divergence was expected. It arrived earlier than planned.

**What the split costs.** What is tested is not quite what is deployed: exchange/queue semantics instead of topic/subscription, and no Azure-specific behaviour (sessions, duplicate detection windows, Entra auth) exercised locally. Phase 7 has to prove the Service Bus path for real.

**What it buys.** RabbitMQ starts in **3 seconds** against the emulator's five minutes and 2.35 GB SQL sidecar, and the management UI makes exchanges, bindings, consumer counts and the dead-letter queue directly inspectable — which is how every bug below was found.

**The unplanned benefit:** running two transports proved the application code is genuinely transport-agnostic. Not one handler, message, endpoint or entity changed. Everything that needed to know lives in `MessagingExtensions`, which picks the transport from the connection string (`amqp://` versus `Endpoint=sb://`) so a service cannot be pointed at one broker while configured for the other.

## Five bugs worth recording

Each cost real time and each is a trap worth recognising again:

1. **Aspire resource names are unique across all resource types.** A subscription named `ordering` collides with the project named `ordering`, and the AppHost refuses to start.
2. **Wolverine 6 moved its runtime compiler out of the core package.** Without `WolverineFx.RuntimeCompilation`, every service starts and then dies with *"no IAssemblyGenerator (Roslyn) is registered"* — which reads like a DI problem and is not.
3. **`IMessageBus` is scoped; a `BackgroundService` is a singleton.** DI validation rejected the captive dependency at startup. The outbox publisher now creates a scope per publish.
4. **Wolverine buffers sends by default.** `PublishAsync` returned before the broker accepted, so the outbox deleted its durable copy too early — a real correctness defect in the outbox, not a tuning detail. Fixed with `SendInline()`, which is why that call is load-bearing.
5. **An opaque lambda factory breaks handler code generation.** `AddScoped<IOrderingDbContext>(sp => sp.GetRequiredService<OrderingDbContext>())` — the idiomatic registration used by Catalog and Basket — forces Wolverine into service location, which its default policy rejects. It throws `InvalidServiceLocationException` *while handling a message*, so the service reports itself healthy while every message retries three times and dead-letters. Registering type-to-type fixes it.

That last one is the most instructive: an HTTP-only service and a message-consuming service have genuinely different constraints on how dependencies may be registered, and nothing warns you at startup.

## Verified

With RabbitMQ:

- Checkout returns **202**, basket emptied atomically with the outbox write
- Order created **2 seconds** after checkout
- Three consecutive checkouts produced exactly three orders — 4 orders, 4 lines, **4 dedupe rows**, one each
- The dedupe primary key rejects a duplicate message id (SQL error **2627**), which is the guarantee the handler relies on
- Empty-basket checkout returns 400; `/orders` unauthenticated returns 401; an Admin sees every order
- Topology correct: fanout exchange, bound queue, 3 consumers, dead-letter queue present

## Known limitations

- **No reaper for stranded outbox entries.** Startup recovery returns in-flight entries to pending, which is sufficient with one instance and not with several. A second Basket instance could leave entries claimed by a dead process indefinitely.
- **`ProcessedMessages` grows forever.** It needs a periodic sweep; the index on `HandledAt` is there for it.
- **Still no automated tests of the messaging.** Everything below was verified by hand. Message flows are timing-dependent and this is exactly the kind of behaviour that does not survive a refactor six weeks from now — the strongest argument yet for Phase 6.
- **Ordering's own outbox does not exist**, because it publishes nothing yet. Phase 4 adds it.
