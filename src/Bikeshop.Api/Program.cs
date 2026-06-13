using Bikeshop.Api.Common;
using Bikeshop.Api.Modules.Accounting;
using Bikeshop.Api.Modules.Inventory;
using Bikeshop.Api.Modules.Invoicing;
using Bikeshop.Api.Modules.Pos;
using Bikeshop.Api.Modules.Procurement;
using Bikeshop.Api.Modules.Workshop;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=bikeshop.db"));

builder.Services.AddScoped<EventStore>();
builder.Services.AddScoped<AccountingService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<InvoicingService>();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1", new() { Title = "Bikeshop API", Version = "v1" }));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await scope.ServiceProvider.GetRequiredService<AccountingService>().EnsureChartOfAccountsAsync();
}

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

// Sondes Kubernetes : liveness (le processus répond) et readiness (la base est joignable).
app.MapGet("/healthz", () => Results.Ok(new { status = "Healthy" })).ExcludeFromDescription();
app.MapGet("/readyz", async (AppDbContext db) =>
    await db.Database.CanConnectAsync()
        ? Results.Ok(new { status = "Ready" })
        : Results.Json(new { status = "NotReady" }, statusCode: StatusCodes.Status503ServiceUnavailable))
    .ExcludeFromDescription();

app.MapAccountingEndpoints();
app.MapInventoryEndpoints();
app.MapWorkshopEndpoints();
app.MapInvoicingEndpoints();
app.MapPosEndpoints();
app.MapProcurementEndpoints();

app.Run();

public partial class Program; // expose le point d'entrée aux tests d'intégration
