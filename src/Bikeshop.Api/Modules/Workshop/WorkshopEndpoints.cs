using Bikeshop.Api.Common;
using Bikeshop.Api.Modules.Inventory;
using Bikeshop.Api.Modules.Invoicing;
using Microsoft.EntityFrameworkCore;

namespace Bikeshop.Api.Modules.Workshop;

public record MechanicRequest(string Name);
public record JobPartRequest(Guid ProductId, decimal Quantity);
public record RepairJobRequest(
    string CustomerName, string? CustomerPhone, string BikeDescription, Guid MechanicId,
    DateTime Start, DateTime End, string? Notes, decimal LaborHours, decimal? LaborRate,
    List<JobPartRequest>? Parts);

public static class WorkshopEndpoints
{
    public static void MapWorkshopEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workshop").WithTags("Atelier");

        group.MapGet("/mechanics", (AppDbContext db) => db.Mechanics.OrderBy(m => m.Name).ToListAsync());

        group.MapPost("/mechanics", async (MechanicRequest req, AppDbContext db) =>
        {
            var mechanic = new Mechanic { Id = Guid.NewGuid(), Name = req.Name };
            db.Mechanics.Add(mechanic);
            await db.SaveChangesAsync();
            return Results.Created($"/api/workshop/mechanics/{mechanic.Id}", mechanic);
        });

        // Planning : interventions sur une plage de dates (par défaut, la semaine à venir).
        group.MapGet("/planning", async (DateTime? from, DateTime? to, AppDbContext db) =>
        {
            var start = from ?? DateTime.UtcNow.Date;
            var end = to ?? start.AddDays(7);
            return await db.RepairJobs
                .Where(j => j.Start < end && j.End > start && j.Status != RepairJobStatus.Cancelled)
                .OrderBy(j => j.Start)
                .ToListAsync();
        });

        group.MapGet("/jobs", (AppDbContext db) =>
            db.RepairJobs.OrderByDescending(j => j.Start).ToListAsync());

        group.MapGet("/jobs/{id:guid}", async (Guid id, AppDbContext db) =>
            await db.RepairJobs.FirstOrDefaultAsync(j => j.Id == id) is { } job
                ? Results.Ok(job) : Results.NotFound());

        group.MapPost("/jobs", async (RepairJobRequest req, AppDbContext db) =>
        {
            var error = await ValidateAsync(req, db);
            if (error is not null) return Results.BadRequest(new { error });

            var job = new RepairJob
            {
                Id = Guid.NewGuid(),
                CustomerName = req.CustomerName,
                CustomerPhone = req.CustomerPhone,
                BikeDescription = req.BikeDescription,
                MechanicId = req.MechanicId,
                Start = req.Start,
                End = req.End,
                Notes = req.Notes,
                LaborHours = req.LaborHours,
                LaborRate = req.LaborRate ?? 45m,
                Parts = await BuildPartsAsync(req.Parts, db),
            };
            db.RepairJobs.Add(job);
            await db.SaveChangesAsync();
            return Results.Created($"/api/workshop/jobs/{job.Id}", job);
        });

        group.MapPut("/jobs/{id:guid}", async (Guid id, RepairJobRequest req, AppDbContext db) =>
        {
            var job = await db.RepairJobs.FirstOrDefaultAsync(j => j.Id == id);
            if (job is null) return Results.NotFound();
            if (job.Status is RepairJobStatus.Invoiced or RepairJobStatus.Cancelled)
                return Results.BadRequest(new { error = "Intervention facturée ou annulée : modification impossible." });

            var error = await ValidateAsync(req, db);
            if (error is not null) return Results.BadRequest(new { error });

            job.CustomerName = req.CustomerName;
            job.CustomerPhone = req.CustomerPhone;
            job.BikeDescription = req.BikeDescription;
            job.MechanicId = req.MechanicId;
            job.Start = req.Start;
            job.End = req.End;
            job.Notes = req.Notes;
            job.LaborHours = req.LaborHours;
            job.LaborRate = req.LaborRate ?? job.LaborRate;
            job.Parts = await BuildPartsAsync(req.Parts, db);
            await db.SaveChangesAsync();
            return Results.Ok(job);
        });

        group.MapPost("/jobs/{id:guid}/start", (Guid id, AppDbContext db) =>
            TransitionAsync(id, db, RepairJobStatus.Scheduled, RepairJobStatus.InProgress));

        group.MapPost("/jobs/{id:guid}/cancel", (Guid id, AppDbContext db) =>
            TransitionAsync(id, db, null, RepairJobStatus.Cancelled));

        // Clôture : les pièces utilisées sortent du stock.
        group.MapPost("/jobs/{id:guid}/complete", async (Guid id, AppDbContext db, InventoryService inventory) =>
        {
            var job = await db.RepairJobs.FirstOrDefaultAsync(j => j.Id == id);
            if (job is null) return Results.NotFound();
            if (job.Status is not (RepairJobStatus.Scheduled or RepairJobStatus.InProgress))
                return Results.BadRequest(new { error = $"Intervention non clôturable (statut : {job.Status})." });

            try
            {
                foreach (var part in job.Parts)
                    await inventory.MoveStockAsync(part.ProductId, -part.Quantity, "WorkshopUse", $"Job {job.Id}");
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

            job.Status = RepairJobStatus.Completed;
            await db.SaveChangesAsync();
            return Results.Ok(job);
        });

        // Facturation de l'intervention : main-d'œuvre + pièces.
        group.MapPost("/jobs/{id:guid}/invoice", async (Guid id, AppDbContext db, InvoicingService invoicing) =>
        {
            var job = await db.RepairJobs.FirstOrDefaultAsync(j => j.Id == id);
            if (job is null) return Results.NotFound();
            if (job.Status != RepairJobStatus.Completed)
                return Results.BadRequest(new { error = $"Seule une intervention clôturée peut être facturée (statut : {job.Status})." });

            var lines = new List<InvoiceLine>();
            if (job.LaborHours > 0)
                lines.Add(new InvoiceLine
                {
                    Description = $"Main-d'œuvre atelier – {job.BikeDescription}",
                    Quantity = job.LaborHours,
                    UnitPrice = job.LaborRate,
                    VatRate = 0.20m,
                    IsService = true,
                });
            lines.AddRange(job.Parts.Select(p => new InvoiceLine
            {
                Description = p.ProductName,
                Quantity = p.Quantity,
                UnitPrice = p.UnitPrice,
                VatRate = p.VatRate,
                IsService = false,
            }));

            try
            {
                var invoice = await invoicing.CreateAsync(job.CustomerName, DateOnly.FromDateTime(DateTime.UtcNow),
                    lines, sourceType: "RepairJob", sourceId: job.Id);
                job.Status = RepairJobStatus.Invoiced;
                job.InvoiceId = invoice.Id;
                await db.SaveChangesAsync();
                return Results.Created($"/api/invoicing/invoices/{invoice.Id}", new { invoiceId = invoice.Id, invoice.Number });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }

    private static async Task<string?> ValidateAsync(RepairJobRequest req, AppDbContext db)
    {
        if (req.End <= req.Start) return "La fin du créneau doit être postérieure au début.";
        if (!await db.Mechanics.AnyAsync(m => m.Id == req.MechanicId)) return "Mécanicien inconnu.";
        foreach (var part in req.Parts ?? [])
            if (!await db.Products.AnyAsync(p => p.Id == part.ProductId))
                return $"Produit {part.ProductId} inconnu.";
        return null;
    }

    private static async Task<List<RepairJobPart>> BuildPartsAsync(List<JobPartRequest>? parts, AppDbContext db)
    {
        var result = new List<RepairJobPart>();
        foreach (var part in parts ?? [])
        {
            var product = await db.Products.FindAsync(part.ProductId);
            result.Add(new RepairJobPart
            {
                ProductId = product!.Id,
                ProductName = product.Name,
                Quantity = part.Quantity,
                UnitPrice = product.UnitPrice,
                VatRate = product.VatRate,
            });
        }
        return result;
    }

    private static async Task<IResult> TransitionAsync(Guid id, AppDbContext db, RepairJobStatus? expected, RepairJobStatus target)
    {
        var job = await db.RepairJobs.FirstOrDefaultAsync(j => j.Id == id);
        if (job is null) return Results.NotFound();
        if (expected is not null && job.Status != expected)
            return Results.BadRequest(new { error = $"Transition impossible depuis le statut {job.Status}." });
        if (target == RepairJobStatus.Cancelled && job.Status is RepairJobStatus.Invoiced)
            return Results.BadRequest(new { error = "Une intervention facturée ne peut pas être annulée." });

        job.Status = target;
        await db.SaveChangesAsync();
        return Results.Ok(job);
    }
}
