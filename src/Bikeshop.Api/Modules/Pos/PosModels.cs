namespace Bikeshop.Api.Modules.Pos;

public enum SessionStatus { Open, Closed }
public enum PaymentMethod { Cash, Card }

public class RegisterSession
{
    public Guid Id { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal OpeningFloat { get; set; }   // fond de caisse
    public decimal? CountedCash { get; set; }   // espèces comptées à la clôture
    public SessionStatus Status { get; set; } = SessionStatus.Open;
}

public class SaleLine
{
    public Guid ProductId { get; set; }
    public required string ProductName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; } // HT
    public decimal VatRate { get; set; }
}

public class Sale
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public required string Number { get; set; }
    public DateTime At { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public List<SaleLine> Lines { get; set; } = [];

    public decimal TotalExclVat => Lines.Sum(l => Math.Round(l.Quantity * l.UnitPrice, 2));
    public decimal TotalVat => Lines.Sum(l => Math.Round(l.Quantity * l.UnitPrice * l.VatRate, 2));
    public decimal TotalInclVat => TotalExclVat + TotalVat;
}
