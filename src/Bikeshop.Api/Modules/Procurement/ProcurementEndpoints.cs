using Bikeshop.Api.Common;
using Bikeshop.Api.Modules.Accounting;
using Bikeshop.Api.Modules.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Bikeshop.Api.Modules.Procurement;

public record SupplierRequest(string Name, string? Email, string? Phone, int? LeadTimeDays);
public record OrderLineRequest(Guid ProductId, decimal Quantity, decimal? UnitCost);
public record CreateOrderRequest(Guid SupplierId, List<OrderLineRequest> Lines);

public static class ProcurementEndpoints
{
    public static void MapProcurementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/procurement").WithTags("Approvisionnement");

        group.MapGet("/suppliers", (AppDbContext db) => db.Suppliers.OrderBy(s => s.Name).ToListAsync());

        group.MapPost("/suppliers", async (SupplierRequest req, AppDbContext db) =>
        {
            var supplier = new Supplier
            {
                Id = Guid.NewGuid(),
                Name = req.Name,
                Email = req.Email,
                Phone = req.Phone,
                LeadTimeDays = req.LeadTimeDays ?? 7,
            };
            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync();
            return Results.Created($"/api/procurement/suppliers/{supplier.Id}", supplier);
        });

        group.MapGet("/orders", async (AppDbContext db) =>
            (await db.PurchaseOrders.OrderByDescending(o => o.CreatedAt).ToListAsync()).Select(ToDto));

        group.MapGet("/orders/{id:guid}", async (Guid id, AppDbContext db) =>
            await db.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == id) is { } order
                ? Results.Ok(ToDto(order)) : Results.NotFound());

        group.MapPost("/orders", async (CreateOrderRequest req, AppDbContext db) =>
        {
            if (!await db.Suppliers.AnyAsync(s => s.Id == req.SupplierId))
                return Results.BadRequest(new { error = "Fournisseur inconnu." });
            if (req.Lines.Count == 0)
                return Results.BadRequest(new { error = "La commande doit comporter au moins une ligne." });

            var order = new PurchaseOrder
            {
                Id = Guid.NewGuid(),
                Number = $"CMD-{DateTime.UtcNow:yyyyMMdd}-{await db.PurchaseOrders.CountAsync() + 1:D4}",
                SupplierId = req.SupplierId,
                CreatedAt = DateTime.UtcNow,
            };
            foreach (var line in req.Lines)
            {
                var product = await db.Products.FindAsync(line.ProductId);
                if (product is null)
                    return Results.BadRequest(new { error = $"Produit {line.ProductId} inconnu." });
                order.Lines.Add(new PurchaseOrderLine
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost ?? product.UnitCost,
                    VatRate = product.VatRate,
                });
            }
            db.PurchaseOrders.Add(order);
            await db.SaveChangesAsync();
            return Results.Created($"/api/procurement/orders/{order.Id}", ToDto(order));
        });

        group.MapPost("/orders/{id:guid}/send", async (Guid id, AppDbContext db) =>
        {
            var order = await db.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (order is null) return Results.NotFound();
            if (order.Status != PurchaseOrderStatus.Draft)
                return Results.BadRequest(new { error = $"Commande non envoyable (statut : {order.Status})." });

            order.Status = PurchaseOrderStatus.Ordered;
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(order));
        });

        // Réception : entrée en stock + écriture comptable (607 + 44566 / 401).
        group.MapPost("/orders/{id:guid}/receive", async (Guid id, AppDbContext db,
            InventoryService inventory, AccountingService accounting) =>
        {
            var order = await db.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (order is null) return Results.NotFound();
            if (order.Status != PurchaseOrderStatus.Ordered)
                return Results.BadRequest(new { error = $"Seule une commande envoyée peut être réceptionnée (statut : {order.Status})." });

            foreach (var line in order.Lines)
                await inventory.MoveStockAsync(line.ProductId, line.Quantity, "PurchaseReception", order.Number);

            var journalLines = new List<JournalLine>
            {
                new(AccountingService.Accounts.GoodsPurchases, order.TotalExclVat, 0),
                new(AccountingService.Accounts.Suppliers, 0, order.TotalInclVat),
            };
            if (order.TotalVat > 0)
                journalLines.Insert(1, new(AccountingService.Accounts.VatDeductible, order.TotalVat, 0));
            await accounting.PostEntryAsync(DateOnly.FromDateTime(DateTime.UtcNow),
                $"Réception commande {order.Number}", order.Number, journalLines);

            order.Status = PurchaseOrderStatus.Received;
            order.ReceivedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(order));
        });

        group.MapPost("/orders/{id:guid}/cancel", async (Guid id, AppDbContext db) =>
        {
            var order = await db.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (order is null) return Results.NotFound();
            if (order.Status == PurchaseOrderStatus.Received)
                return Results.BadRequest(new { error = "Une commande réceptionnée ne peut pas être annulée." });

            order.Status = PurchaseOrderStatus.Cancelled;
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(order));
        });

        // Suggestions de réassort : produits sous leur seuil, regroupés par fournisseur privilégié.
        group.MapGet("/replenishment-suggestions", async (AppDbContext db, InventoryService inventory) =>
        {
            var lowStock = await inventory.GetLowStockAsync();
            var suppliers = await db.Suppliers.ToDictionaryAsync(s => s.Id);
            return lowStock.Select(p => new
            {
                p.Id, p.Sku, p.Name, p.QuantityOnHand, p.ReorderThreshold,
                SuggestedQuantity = Math.Max(p.ReorderThreshold * 2 - p.QuantityOnHand, 1),
                p.PreferredSupplierId,
                PreferredSupplierName = p.PreferredSupplierId is { } sid && suppliers.TryGetValue(sid, out var s)
                    ? s.Name : null,
            });
        });
    }

    private static object ToDto(PurchaseOrder o) => new
    {
        o.Id, o.Number, o.SupplierId,
        Status = o.Status.ToString(),
        o.CreatedAt, o.ReceivedAt, o.Lines,
        o.TotalExclVat, o.TotalVat, o.TotalInclVat,
    };
}
