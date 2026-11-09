var builder = DistributedApplication.CreateBuilder(args);

// ── Databases ────────────────────────────────────────────────────────────────────
// Both live on the developer's local SQL Server instance, so they are referenced as
// existing connection strings. Aspire injects and displays them but does not manage
// their lifecycle. Each service gets exactly one, and no service gets another's.
var catalogDb = builder.AddConnectionString("catalogdb");
var identityDb = builder.AddConnectionString("identitydb");
var orderingDb = builder.AddConnectionString("orderingdb");

// Basket is the one service that is not relational. Unlike the two above, Aspire *does*
// own this one's lifecycle: it pulls the image, starts the container, wires the connection
// string, and stops it on shutdown. The data volume is what makes baskets survive a
// restart; without it every `aspire run` would start everyone's basket empty, and the
// 30-day expiry the design talks about would never be observable.
var basketCache = builder.AddRedis("basketcache")
    .WithDataVolume()
    .WithRedisInsight();

// ── Messaging ────────────────────────────────────────────────────────────────────
// Two brokers, chosen by execution mode: RabbitMQ when you run locally, Azure Service Bus
// when the AppHost is published.
//
// This is not the arrangement anyone would pick on purpose — what you test is not quite
// what you run. It was forced: the Azure Service Bus emulator will not deliver a topic
// message without a subscription rule, and crashes with "Out of memory." at entity sync
// if given one. Reproduced repeatedly; see ADR 0008 for the full diagnosis.
//
// The compensation is that nothing in any service knows which broker it is talking to.
// ShopLite.ServiceDefaults/MessagingExtensions.cs decides from the connection string, and
// not one handler, message or endpoint differs between the two.
IResourceBuilder<IResourceWithConnectionString> messaging;

if (builder.ExecutionContext.IsRunMode)
{
    // The management plugin is worth the few extra MB: it gives a browser UI for the
    // exchange, the queue, the binding and the dead-letter queue, which turns "the
    // message vanished" from guesswork into something you can look at. The data volume
    // keeps undelivered messages across restarts.
    messaging = builder.AddRabbitMQ("messaging")
        .WithManagementPlugin()
        .WithDataVolume();
}
else
{
    // The deployment target. Entities are declared rather than created at runtime, so the
    // topology is reviewable here instead of inferred from whichever service starts first.
    var serviceBus = builder.AddAzureServiceBus("messaging");

    // An *event*: Basket states a fact and does not know who cares. A topic, so
    // subscribers can be added without Basket changing. Ordering is the only one today.
    var basketCheckedOut = serviceBus.AddServiceBusTopic("basket-checked-out");

    // The name must not clash with the "ordering" project below: Aspire keeps one flat
    // namespace for every resource, whatever its type.
    basketCheckedOut.AddServiceBusSubscription("ordering-service");

    messaging = serviceBus;
}

// ── Shared JWT settings ──────────────────────────────────────────────────────────
// Identity signs tokens with these; every other service validates with them. Declaring
// them once here is what stops issuer/audience/key drifting between services.
var jwtKey = builder.AddParameter("JwtKey", secret: true);
const string jwtIssuer = "shoplite-identity";
const string jwtAudience = "shoplite-api";

// Where the Angular dev server runs, for CORS.
const string webOrigin = "http://localhost:4200";

// ── Services ─────────────────────────────────────────────────────────────────────
var identity = builder.AddProject<Projects.Identity_Api>("identity")
    .WithReference(identityDb)
    .WithEnvironment("Jwt__Issuer", jwtIssuer)
    .WithEnvironment("Jwt__Audience", jwtAudience)
    .WithEnvironment("Jwt__ExpiryMinutes", "120")
    .WithEnvironment("Jwt__Key", jwtKey)
    .WithEnvironment("Cors__AllowedOrigins", webOrigin);

var catalog = builder.AddProject<Projects.Catalog_Api>("catalog")
    .WithReference(catalogDb)
    .WithEnvironment("Jwt__Issuer", jwtIssuer)
    .WithEnvironment("Jwt__Audience", jwtAudience)
    .WithEnvironment("Jwt__Key", jwtKey)
    .WithEnvironment("Cors__AllowedOrigins", webOrigin);

// Basket is the only service that calls another one. WithReference(catalog) is what makes
// that possible: it injects the service-discovery configuration behind the name "catalog",
// so Basket resolves "https+http://catalog" at request time instead of holding a port.
var basket = builder.AddProject<Projects.Basket_Api>("basket")
    .WithReference(basketCache)
    .WaitFor(basketCache)
    .WithReference(catalog)

    // Basket publishes BasketCheckedOut. WaitFor matters here: the emulator halts itself
    // if its own SQL dependency is not ready, so starting a publisher against a
    // half-started broker produces confusing connection errors rather than a clean wait.
    .WithReference(messaging)
    .WaitFor(messaging)
    .WithEnvironment("Jwt__Issuer", jwtIssuer)
    .WithEnvironment("Jwt__Audience", jwtAudience)
    .WithEnvironment("Jwt__Key", jwtKey)
    .WithEnvironment("Cors__AllowedOrigins", webOrigin);

// Ordering consumes BasketCheckedOut and owns no synchronous dependency on anyone —
// no WithReference(catalog), no WithReference(basket). That absence is the point of the
// phase: it learns what it needs from a message, so it starts, runs and stays healthy
// whether or not Basket and Catalog are up.
var ordering = builder.AddProject<Projects.Ordering_Api>("ordering")
    .WithReference(orderingDb)
    .WithReference(messaging)
    .WaitFor(messaging)
    .WithEnvironment("Jwt__Issuer", jwtIssuer)
    .WithEnvironment("Jwt__Audience", jwtAudience)
    .WithEnvironment("Jwt__Key", jwtKey)
    .WithEnvironment("Cors__AllowedOrigins", webOrigin);

var gateway = builder.AddProject<Projects.ShopLite_Gateway>("gateway")
    .WithReference(identity)
    .WithReference(catalog)
    .WithReference(basket)
    .WithReference(ordering)
    .WithEnvironment("ReverseProxy__Clusters__identity__Destinations__identity__Address", identity.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__catalog__Destinations__catalog__Address", catalog.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__basket__Destinations__basket__Address", basket.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__ordering__Destinations__ordering__Address", ordering.GetEndpoint("http"))
    .WithEnvironment("Cors__AllowedOrigins", webOrigin)
    .WithExternalHttpEndpoints();

// ── Frontend ─────────────────────────────────────────────────────────────────────
// Aspire runs `npm start` and treats the Angular dev server as another resource: it
// appears in the dashboard with its own logs and lifecycle. Browser API calls go through
// the gateway, while the backend APIs remain private resources.
builder.AddNpmApp("web", "../../Web/shoplite-web", "start")
    .WithReference(gateway)
    .WithHttpEndpoint(port: 4200, env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
