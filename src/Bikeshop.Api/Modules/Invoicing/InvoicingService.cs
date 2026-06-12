using Bikeshop.Api.Common;
using Bikeshop.Api.Modules.Accounting;
using Microsoft.EntityFrameworkCore;

namespace Bikeshop.Api.Modules.Invoicing;

/// <summary>Facturation : à l'émission, l'écriture de vente est passée en comptabilité
/// (411 / 706-707 / 44571) ; à l'encaissement, le règlement (512 ou 530 / 411).</summary>
public class InvoicingService(AppDbContext db, AccountingService accounting)
{
    public async Task<Invoice> CreateAsync(string customerName, DateOnly issueDate, List<InvoiceLine> lines,
        string? sourceType = null, Guid? sourceId = null)
    {
        if (lines.Count == 0)
            throw new InvalidOperationException("Une facture doit comporter au moins une ligne.");

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            Number = await NextNumberAsync(issueDate.Year),
            CustomerName = customerName,
            IssueDate = issueDate,
            DueDate = issueDate.AddDays(30),
            SourceType = sourceType,
            SourceId = sourceId,
            Lines = lines,
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice;
    }

    public async Task<Invoice> IssueAsync(Guid invoiceId)
    {
        var invoice = await GetRequiredAsync(invoiceId);
        if (invoice.Status != InvoiceStatus.Draft)
            throw new InvalidOperationException($"Seule une facture en brouillon peut être émise (statut : {invoice.Status}).");

        var lines = new List<JournalLine>
        {
            new(AccountingService.Accounts.Customers, invoice.TotalInclVat, 0),
        };
        var services = invoice.Lines.Where(l => l.IsService).Sum(l => Math.Round(l.Quantity * l.UnitPrice, 2));
        var goods = invoice.Lines.Where(l => !l.IsService).Sum(l => Math.Round(l.Quantity * l.UnitPrice, 2));
        if (services > 0) lines.Add(new(AccountingService.Accounts.ServiceSales, 0, services));
        if (goods > 0) lines.Add(new(AccountingService.Accounts.GoodsSales, 0, goods));
        if (invoice.TotalVat > 0) lines.Add(new(AccountingService.Accounts.VatCollected, 0, invoice.TotalVat));

        await accounting.PostEntryAsync(invoice.IssueDate,
            $"Facture {invoice.Number} – {invoice.CustomerName}", invoice.Number, lines);

        invoice.Status = InvoiceStatus.Issued;
        await db.SaveChangesAsync();
        return invoice;
    }

    public async Task<Invoice> PayAsync(Guid invoiceId, string paymentMethod, DateOnly paymentDate)
    {
        var invoice = await GetRequiredAsync(invoiceId);
        if (invoice.Status != InvoiceStatus.Issued)
            throw new InvalidOperationException($"Seule une facture émise peut être encaissée (statut : {invoice.Status}).");

        var treasuryAccount = paymentMethod.Equals("cash", StringComparison.OrdinalIgnoreCase)
            ? AccountingService.Accounts.CashBox
            : AccountingService.Accounts.Bank;

        await accounting.PostEntryAsync(paymentDate,
            $"Règlement facture {invoice.Number} – {invoice.CustomerName}", invoice.Number,
            [
                new(treasuryAccount, invoice.TotalInclVat, 0),
                new(AccountingService.Accounts.Customers, 0, invoice.TotalInclVat),
            ]);

        invoice.Status = InvoiceStatus.Paid;
        await db.SaveChangesAsync();
        return invoice;
    }

    private async Task<Invoice> GetRequiredAsync(Guid id) =>
        await db.Invoices.FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new InvalidOperationException($"Facture {id} introuvable.");

    private async Task<string> NextNumberAsync(int year)
    {
        var prefix = $"FAC-{year}-";
        var count = await db.Invoices.CountAsync(i => i.Number.StartsWith(prefix));
        return $"{prefix}{count + 1:D4}";
    }
}
