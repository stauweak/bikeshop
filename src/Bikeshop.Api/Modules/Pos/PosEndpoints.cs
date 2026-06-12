using Bikeshop.Api.Common;
using Bikeshop.Api.Modules.Accounting;
using Bikeshop.Api.Modules.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Bikeshop.Api.Modules.Pos;

public record OpenSessionRequest(decimal OpeningFloat);
public record CloseSessionRequest(decimal CountedCash);
public record SaleLineRequest(Guid ProductId, decimal Quantity);
public record CreateSaleRequest(PaymentMethod PaymentMethod, List<SaleLineRequest> Lines);

public static class PosEndpoints
{
    public static void MapPosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pos").WithTags("Caisse");

        group.MapGet("/sessions", (AppDbContext db) =>
            db.RegisterSessions.OrderByDescending(s => s.OpenedAt).ToListAsync());

        group.MapGet("/sessions/current", async (AppDbContext db) =>
            await db.RegisterSessions.FirstOrDefaultAsync(s => s.Status == SessionStatus.Open) is { } session
                ? Results.Ok(session) : Results.NotFound());

        group.MapPost("/sessions/open", async (OpenSessionRequest req, AppDbContext db) =>
        {
            if (await db.RegisterSessions.AnyAsync(s => s.Status == SessionStatus.Open))
                return Results.BadRequest(new { error = "Une session de caisse est déjà ouverte." });

            var session = new RegisterSession
            {
                Id = Guid.NewGuid(),
                OpenedAt = DateTime.UtcNow,
                OpeningFloat = req.OpeningFloat,
            };
            db.RegisterSessions.Add(session);
            await db.SaveChangesAsync();
            return Results.Created($"/api/pos/sessions/{session.Id}", session);
        });

        group.MapPost("/sessions/{id:guid}/close", async (Guid id, CloseSessionRequest req, AppDbContext db) =>
        {
            var session = await db.RegisterSessions.FindAsync(id);
            if (session is null) return Results.NotFound();
            if (session.Status == SessionStatus.Closed)
                return Results.BadRequest(new { error = "Session déjà clôturée." });

            session.Status = SessionStatus.Closed;
            session.ClosedAt = DateTime.UtcNow;
            session.CountedCash = req.CountedCash;
            await db.SaveChangesAsync();
            return Results.Ok(await BuildSummaryAsync(session, db));
        });

        // Ticket Z / X : synthèse de la session.
        group.MapGet("/sessions/{id:guid}/summary", async (Guid id, AppDbContext db) =>
        {
            var session = await db.RegisterSessions.FindAsync(id);
            return session is null ? Results.NotFound() : Results.Ok(await BuildSummaryAsync(session, db));
        });

        group.MapGet("/sales", async (Guid? sessionId, AppDbContext db) =>
        {
            var query = db.Sales.AsQueryable();
            if (sessionId is not null) query = query.Where(s => s.SessionId == sessionId);
            return (await query.OrderByDescending(s => s.At).Take(200).ToListAsync()).Select(ToDto);
        });

        // Vente : décrémente le stock et passe l'écriture comptable (530/512 ; 707 ; 44571).
        group.MapPost("/sales", async (CreateSaleRequest req, AppDbContext db,
            InventoryService inventory, AccountingService accounting) =>
        {
            var session = await db.RegisterSessions.FirstOrDefaultAsync(s => s.Status == SessionStatus.Open);
            if (session is null)
                return Results.BadRequest(new { error = "Aucune session de caisse ouverte : ouvrez la caisse d'abord." });
            if (req.Lines.Count == 0)
                return Results.BadRequest(new { error = "Le ticket est vide." });

            var sale = new Sale
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Number = $"TCK-{DateTime.UtcNow:yyyyMMdd}-{await db.Sales.CountAsync() + 1:D4}",
                At = DateTime.UtcNow,
                PaymentMethod = req.PaymentMethod,
            };

            try
            {
                foreach (var line in req.Lines)
                {
                    var product = await inventory.MoveStockAsync(line.ProductId, -line.Quantity, "Sale", sale.Number);
                    sale.Lines.Add(new SaleLine
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Quantity = line.Quantity,
                        UnitPrice = product.UnitPrice,
                        VatRate = product.VatRate,
                    });
                }

                var treasury = req.PaymentMethod == PaymentMethod.Cash
                    ? AccountingService.Accounts.CashBox
                    : AccountingService.Accounts.Bank;
                var journalLines = new List<JournalLine>
                {
                    new(treasury, sale.TotalInclVat, 0),
                    new(AccountingService.Accounts.GoodsSales, 0, sale.TotalExclVat),
                };
                if (sale.TotalVat > 0)
                    journalLines.Add(new(AccountingService.Accounts.VatCollected, 0, sale.TotalVat));
                await accounting.PostEntryAsync(DateOnly.FromDateTime(sale.At),
                    $"Vente caisse {sale.Number}", sale.Number, journalLines);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

            db.Sales.Add(sale);
            await db.SaveChangesAsync();
            return Results.Created($"/api/pos/sales/{sale.Id}", ToDto(sale));
        });
    }

    private static async Task<object> BuildSummaryAsync(RegisterSession session, AppDbContext db)
    {
        var sales = await db.Sales.Where(s => s.SessionId == session.Id).ToListAsync();
        var cashTotal = sales.Where(s => s.PaymentMethod == PaymentMethod.Cash).Sum(s => s.TotalInclVat);
        var cardTotal = sales.Where(s => s.PaymentMethod == PaymentMethod.Card).Sum(s => s.TotalInclVat);
        var expectedCash = session.OpeningFloat + cashTotal;
        return new
        {
            session.Id, session.OpenedAt, session.ClosedAt,
            Status = session.Status.ToString(),
            session.OpeningFloat,
            SaleCount = sales.Count,
            CashTotal = cashTotal,
            CardTotal = cardTotal,
            Total = cashTotal + cardTotal,
            ExpectedCash = expectedCash,
            session.CountedCash,
            CashDifference = session.CountedCash is { } counted ? counted - expectedCash : (decimal?)null,
        };
    }

    private static object ToDto(Sale s) => new
    {
        s.Id, s.SessionId, s.Number, s.At,
        PaymentMethod = s.PaymentMethod.ToString(),
        s.Lines, s.TotalExclVat, s.TotalVat, s.TotalInclVat,
    };
}
