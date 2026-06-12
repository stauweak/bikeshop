namespace Bikeshop.Api.Modules.Workshop;

public class Mechanic
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
}

public enum RepairJobStatus { Scheduled, InProgress, Completed, Invoiced, Cancelled }

public class RepairJobPart
{
    public Guid ProductId { get; set; }
    public required string ProductName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; } // HT
    public decimal VatRate { get; set; }
}

public class RepairJob
{
    public Guid Id { get; set; }
    public required string CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public required string BikeDescription { get; set; }
    public Guid MechanicId { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public RepairJobStatus Status { get; set; } = RepairJobStatus.Scheduled;
    public string? Notes { get; set; }
    public decimal LaborHours { get; set; }
    public decimal LaborRate { get; set; } = 45m; // taux horaire HT
    public List<RepairJobPart> Parts { get; set; } = [];
    public Guid? InvoiceId { get; set; }
}
