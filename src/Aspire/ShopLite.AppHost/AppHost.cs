var builder = DistributedApplication.CreateBuilder(args);

// ── Databases ────────────────────────────────────────────────────────────────────
// Both live on the developer's local SQL Server instance, so they are referenced as
// existing connection strings. Aspire injects and displays them but does not manage
// their lifecycle. Each service gets exactly one, and no service gets another's.
var catalogDb = builder.AddConnectionString("catalogdb");
var identityDb = builder.AddConnectionString("identitydb");

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

// ── Frontend ─────────────────────────────────────────────────────────────────────
// Aspire runs `npm start` and treats the Angular dev server as another resource: it
// appears in the dashboard with its own logs and lifecycle. The API URLs are passed in
// as environment variables rather than hardcoded in the Angular source.
builder.AddNpmApp("web", "../../Web/shoplite-web", "start")
    .WithReference(identity)
    .WithReference(catalog)
    .WithEnvironment("NG_APP_IDENTITY_URL", identity.GetEndpoint("https"))
    .WithEnvironment("NG_APP_CATALOG_URL", catalog.GetEndpoint("https"))
    .WithHttpEndpoint(port: 4200, env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
