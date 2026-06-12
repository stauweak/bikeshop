namespace Bikeshop.Api.Modules.Inventory;

public enum ProductCategory { Bike, Component, Accessory, Consumable }

public class Product
{
    public Guid Id { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public ProductCategory Category { get; set; }
    public decimal UnitPrice { get; set; }      // prix de vente HT
    public decimal UnitCost { get; set; }       // coût d'achat HT
    public decimal VatRate { get; set; } = 0.20m;
    public decimal QuantityOnHand { get; set; }
    public decimal ReorderThreshold { get; set; }
    public Guid? PreferredSupplierId { get; set; }
}

public class StockMovement
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }       // positif = entrée, négatif = sortie
    public required string Reason { get; set; } // Sale, PurchaseReception, WorkshopUse, Adjustment
    public string? Reference { get; set; }
    public DateTime OccurredAt { get; set; }
}
