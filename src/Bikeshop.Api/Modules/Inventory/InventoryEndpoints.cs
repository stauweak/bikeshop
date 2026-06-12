using Bikeshop.Api.Common;
using Microsoft.EntityFrameworkCore;

namespace Bikeshop.Api.Modules.Inventory;

public record ProductRequest(
    string Sku, string Name, ProductCategory Category, decimal UnitPrice, decimal UnitCost,
    decimal VatRate, decimal QuantityOnHand, decimal ReorderThreshold, Guid? PreferredSupplierId);

public record StockAdjustmentRequest(decimal Quantity, string? Reason, string? Reference);

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory").WithTags("Stocks");

        group.MapGet("/products", (AppDbContext db) =>
            db.Products.OrderBy(p => p.Name).ToListAsync());

        group.MapGet("/products/{id:guid}", async (Guid id, AppDbContext db) =>
            await db.Products.FindAsync(id) is { } p ? Results.Ok(p) : Results.NotFound());

        group.MapPost("/products", async (ProductRequest req, AppDbContext db) =>
        {
            if (await db.Products.AnyAsync(p => p.Sku == req.Sku))
                return Results.BadRequest(new { error = $"La référence {req.Sku} existe déjà." });

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Sku = req.Sku,
                Name = req.Name,
                Category = req.Category,
                UnitPrice = req.UnitPrice,
                UnitCost = req.UnitCost,
                VatRate = req.VatRate,
                QuantityOnHand = req.QuantityOnHand,
                ReorderThreshold = req.ReorderThreshold,
                PreferredSupplierId = req.PreferredSupplierId,
            };
            db.Products.Add(product);
            await db.SaveChangesAsync();
            return Results.Created($"/api/inventory/products/{product.Id}", product);
        });

        group.MapPut("/products/{id:guid}", async (Guid id, ProductRequest req, AppDbContext db) =>
        {
            var product = await db.Products.FindAsync(id);
            if (product is null) return Results.NotFound();

            product.Sku = req.Sku;
            product.Name = req.Name;
            product.Category = req.Category;
            product.UnitPrice = req.UnitPrice;
            product.UnitCost = req.UnitCost;
            product.VatRate = req.VatRate;
            product.ReorderThreshold = req.ReorderThreshold;
            product.PreferredSupplierId = req.PreferredSupplierId;
            await db.SaveChangesAsync();
            return Results.Ok(product);
        });

        group.MapPost("/products/{id:guid}/adjust", async (Guid id, StockAdjustmentRequest req, InventoryService svc) =>
        {
            try
            {
                var product = await svc.MoveStockAsync(id, req.Quantity, req.Reason ?? "Adjustment", req.Reference);
                return Results.Ok(product);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/movements", (AppDbContext db) =>
            db.StockMovements.OrderByDescending(m => m.OccurredAt).Take(200).ToListAsync());

        group.MapGet("/low-stock", (InventoryService svc) => svc.GetLowStockAsync());
    }
}
