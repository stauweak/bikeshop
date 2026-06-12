namespace Bikeshop.Api.Modules.Invoicing;

public enum InvoiceStatus { Draft, Issued, Paid, Cancelled }

public class InvoiceLine
{
    public required string Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; } // HT
    public decimal VatRate { get; set; }
    public bool IsService { get; set; } // ventile en 706 (prestation) ou 707 (marchandise)
}

public class Invoice
{
    public Guid Id { get; set; }
    public required string Number { get; set; }
    public required string CustomerName { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public string? SourceType { get; set; } // ex. "RepairJob"
    public Guid? SourceId { get; set; }
    public List<InvoiceLine> Lines { get; set; } = [];

    public decimal TotalExclVat => Lines.Sum(l => Math.Round(l.Quantity * l.UnitPrice, 2));
    public decimal TotalVat => Lines.Sum(l => Math.Round(l.Quantity * l.UnitPrice * l.VatRate, 2));
    public decimal TotalInclVat => TotalExclVat + TotalVat;
}
