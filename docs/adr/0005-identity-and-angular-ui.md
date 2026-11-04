# 5. Identity service and Angular UI

**Date:** 2026-11-04
**Status:** Accepted
**Supersedes:** the Keycloak choice recorded in ARCHITECTURE.md §10 and §12.

## Context

The project needed a UI and role-based access control. The original plan had Keycloak as the identity provider in Phase 5, and no UI at all.

## Decisions

### ASP.NET Core Identity instead of Keycloak

A new `Identity.Api` service owns users, roles and token issuing, backed by `ShopLite_Identity` on the local SQL Server instance.

Keycloak is the more production-realistic choice, but its schema is Keycloak's own: there is no database to design, and nothing to inspect in SSMS. ASP.NET Core Identity gives a schema you own (seven `AspNet*` tables), no extra container, and a clear view of how a JWT is actually minted and validated. For a learning project that is the better trade.

### Identity is one project, not four layers

Every other service uses Domain/Application/Infrastructure/Api per [ADR 0004](0004-clean-architecture-per-service.md). Identity does not.

ASP.NET Core Identity supplies both the entities (`IdentityUser`, `IdentityRole`) and the use cases (`UserManager`, `SignInManager`). There is no domain model of our own to protect, so a Domain layer would hold nothing and an Application layer would be a pass-through to framework types. Four projects here would be ceremony with no payoff.

### Roles as token claims, not a lookup

Roles are embedded in the JWT when it is issued. Catalog reads them from the signed token and never calls Identity.

- **Gain:** no network hop per request, and Catalog keeps working when Identity is down.
- **Cost:** a role change takes effect only when the user obtains a new token. The Users screen says so explicitly.

This trade-off is the normal one for distributed systems, and it is worth understanding before Phase 3 makes the same bargain with messages.

### Symmetric signing key, for now

Identity signs with HMAC-SHA256 using `Jwt:Key`, supplied by the AppHost to every service. This means **every service holds a key that can also mint tokens** — a service compromise becomes a token-forgery capability.

Correct for production: the issuer signs with a private key, services validate with the public half fetched from a JWKS endpoint, and no service but Identity can create a token. Deferred because it adds key management with nothing to learn about microservices in exchange. Noted here so the shortcut is recorded, not forgotten.

### Policies in ServiceDefaults

`Roles` and `Policies` are constants in `ShopLite.ServiceDefaults`, with the policy definitions alongside. Endpoints say `.RequireAuthorization(Policies.ManageCatalog)` rather than listing roles inline, so changing who may edit the catalog is one line in one place rather than a search across six services. Role names as constants also mean a typo fails to compile instead of silently denying access.

### Angular CLI, which already runs on Vite

Angular 22's `@angular/build` builder uses Vite for the dev server and esbuild for production builds, so "Angular with Vite" needs no separate Vite project. Going raw Vite with a community Angular plugin would have given up `ng generate` and schematics for control of a config file we have no reason to touch.

Bootstrap 5.3.8 is compiled from its Sass source in `styles.scss` rather than linked as prebuilt CSS, so theme variables can be overridden through `@use ... with (...)`.

### Three layers of access control, deliberately redundant

| Layer | Mechanism | Purpose |
|---|---|---|
| Navigation | `@if (isAdmin())` in templates | Don't offer what would be refused |
| Routing | `roleGuard(Role.Admin)` on routes | Don't load a page that cannot work |
| **API** | `.RequireAuthorization(...)` | **The only one that is security** |

The first two are convenience. Anything in a browser can be edited, so the server check is the real boundary; the UI checks exist so honest users are not shown dead ends.

### CORS now, gateway later

Each API allows the Angular dev origin via `CorsExtensions`. This exists only because the browser currently talks to each service directly. Phase 2's YARP gateway gives the UI one same-origin entry point and this can be deleted — which is a less obvious but real reason gateways exist.

## Consequences

- Auth arrives before messaging, reordering the roadmap. Phase 5's identity work is now largely done.
- Two databases: `ShopLite_Catalog` and `ShopLite_Identity`, still one per service.
- Catalog's product endpoints now require authentication, so the earlier unauthenticated `.http` requests return 401 until a token is supplied.
- The Angular bundle exceeds Angular's default 500 kB budget because of full Bootstrap CSS. Budget raised to 900 kB rather than cherry-picking Bootstrap modules.
- Three seeded accounts exist for development, all with password `Passw0rd!`. They are created only in the Development environment.
