using Bikeshop.Api.Common;
using Microsoft.EntityFrameworkCore;

namespace Bikeshop.Api.Modules.Invoicing;

public record InvoiceLineRequest(string Description, decimal Quantity, decimal UnitPrice, decimal VatRate, bool IsService);
public record CreateInvoiceRequest(string CustomerName, DateOnly? IssueDate, List<InvoiceLineRequest> Lines);
public record PayInvoiceRequest(string PaymentMethod, DateOnly? PaymentDate);

public static class InvoicingEndpoints
{
    public static void MapInvoicingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/invoicing").WithTags("Facturation");

        group.MapGet("/invoices", async (AppDbContext db) =>
            (await db.Invoices.OrderByDescending(i => i.Number).ToListAsync())
                .Select(ToDto));

        group.MapGet("/invoices/{id:guid}", async (Guid id, AppDbContext db) =>
            await db.Invoices.FirstOrDefaultAsync(i => i.Id == id) is { } invoice
                ? Results.Ok(ToDto(invoice))
                : Results.NotFound());

        group.MapPost("/invoices", async (CreateInvoiceRequest req, InvoicingService svc) =>
        {
            try
            {
                var lines = req.Lines.Select(l => new InvoiceLine
                {
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    VatRate = l.VatRate,
                    IsService = l.IsService,
                }).ToList();
                var invoice = await svc.CreateAsync(req.CustomerName,
                    req.IssueDate ?? DateOnly.FromDateTime(DateTime.UtcNow), lines);
                return Results.Created($"/api/invoicing/invoices/{invoice.Id}", ToDto(invoice));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/invoices/{id:guid}/issue", async (Guid id, InvoicingService svc) =>
        {
            try { return Results.Ok(ToDto(await svc.IssueAsync(id))); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapPost("/invoices/{id:guid}/pay", async (Guid id, PayInvoiceRequest req, InvoicingService svc) =>
        {
            try
            {
                var invoice = await svc.PayAsync(id, req.PaymentMethod,
                    req.PaymentDate ?? DateOnly.FromDateTime(DateTime.UtcNow));
                return Results.Ok(ToDto(invoice));
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });
    }

    private static object ToDto(Invoice i) => new
    {
        i.Id, i.Number, i.CustomerName, i.IssueDate, i.DueDate,
        Status = i.Status.ToString(),
        i.SourceType, i.SourceId, i.Lines,
        i.TotalExclVat, i.TotalVat, i.TotalInclVat,
    };
}
