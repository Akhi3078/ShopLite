using Identity.Api.Endpoints;
using Identity.Api.Persistence;
using Identity.Api.Services;
using Microsoft.AspNetCore.Identity;
using Scalar.AspNetCore;
using ShopLite.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSqlServerDbContext<ShopLiteIdentityDbContext>("identitydb");

// Identity needs to validate tokens too, for its own /auth/me and /users endpoints.
builder.AddShopLiteAuthentication();

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;

        // Relaxed for local development. Production would keep the defaults and add
        // lockout, two-factor and a real password policy.
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireDigit = true;
    })
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<ShopLiteIdentityDbContext>();

builder.Services.AddScoped<TokenService>();

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.MapDefaultEndpoints();

// Order matters: authentication identifies the caller, authorization then decides.
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    await app.Services.SeedIdentityAsync();
}

app.MapAuth();
app.MapUsers();

app.Run();
