namespace Bikeshop.Api.Modules.Procurement;

public class Supplier
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int LeadTimeDays { get; set; } = 7;
}

public enum PurchaseOrderStatus { Draft, Ordered, Received, Cancelled }

public class PurchaseOrderLine
{
    public Guid ProductId { get; set; }
    public required string ProductName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; } // HT
    public decimal VatRate { get; set; }
}

public class PurchaseOrder
{
    public Guid Id { get; set; }
    public required string Number { get; set; }
    public Guid SupplierId { get; set; }
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public List<PurchaseOrderLine> Lines { get; set; } = [];

    public decimal TotalExclVat => Lines.Sum(l => Math.Round(l.Quantity * l.UnitCost, 2));
    public decimal TotalVat => Lines.Sum(l => Math.Round(l.Quantity * l.UnitCost * l.VatRate, 2));
    public decimal TotalInclVat => TotalExclVat + TotalVat;
}
