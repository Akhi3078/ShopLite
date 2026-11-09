using ShopLite.ServiceDefaults;
using Yarp.ReverseProxy;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddShopLiteCors();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseShopLiteCors();
app.MapReverseProxy();

app.Run();
