using Bikeshop.Api.Common;
using Microsoft.EntityFrameworkCore;

namespace Bikeshop.Api.Modules.Inventory;

/// <summary>Gestion des stocks ; point d'entrée unique pour tous les mouvements,
/// utilisé aussi par la caisse, l'atelier et l'approvisionnement.</summary>
public class InventoryService(AppDbContext db)
{
    public async Task<Product> MoveStockAsync(Guid productId, decimal quantity, string reason, string? reference)
    {
        var product = await db.Products.FindAsync(productId)
            ?? throw new InvalidOperationException($"Produit {productId} introuvable.");

        if (quantity < 0 && product.QuantityOnHand + quantity < 0)
            throw new InvalidOperationException(
                $"Stock insuffisant pour « {product.Name} » : {product.QuantityOnHand} en stock, {-quantity} demandé.");

        product.QuantityOnHand += quantity;
        db.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Quantity = quantity,
            Reason = reason,
            Reference = reference,
            OccurredAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return product;
    }

    public async Task<List<Product>> GetLowStockAsync() =>
        (await db.Products.ToListAsync())
            .Where(p => p.QuantityOnHand <= p.ReorderThreshold)
            .ToList();
}
