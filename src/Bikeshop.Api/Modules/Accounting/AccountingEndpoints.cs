namespace Bikeshop.Api.Modules.Accounting;

public record OpenAccountRequest(string Number, string Name, string Type);
public record PostEntryRequest(DateOnly Date, string Label, string? Reference, List<JournalLine> Lines);

public static class AccountingEndpoints
{
    public static void MapAccountingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting").WithTags("Comptabilité");

        group.MapGet("/accounts", (AccountingService svc) => svc.GetTrialBalanceAsync());

        group.MapPost("/accounts", async (OpenAccountRequest req, AccountingService svc) =>
        {
            try
            {
                await svc.OpenAccountAsync(req.Number, req.Name, req.Type);
                return Results.Created($"/api/accounting/accounts/{req.Number}", req);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/journal", (AccountingService svc) => svc.GetJournalAsync());

        group.MapPost("/entries", async (PostEntryRequest req, AccountingService svc) =>
        {
            try
            {
                var id = await svc.PostEntryAsync(req.Date, req.Label, req.Reference, req.Lines);
                return Results.Created($"/api/accounting/journal", new { entryId = id });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/balance", (AccountingService svc) => svc.GetTrialBalanceAsync());

        group.MapGet("/accounts/{number}/ledger", async (string number, AccountingService svc) =>
        {
            var ledger = await svc.GetLedgerAsync(number);
            return ledger is null ? Results.NotFound() : Results.Ok(ledger);
        });
    }
}
