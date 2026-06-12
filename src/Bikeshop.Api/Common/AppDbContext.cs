using Bikeshop.Api.Modules.Accounting;
using Bikeshop.Api.Modules.Inventory;
using Bikeshop.Api.Modules.Invoicing;
using Bikeshop.Api.Modules.Pos;
using Bikeshop.Api.Modules.Procurement;
using Bikeshop.Api.Modules.Workshop;
using Microsoft.EntityFrameworkCore;

namespace Bikeshop.Api.Common;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Comptabilité (event sourcing)
    public DbSet<StoredEvent> Events => Set<StoredEvent>();

    // Stocks
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    // Atelier
    public DbSet<Mechanic> Mechanics => Set<Mechanic>();
    public DbSet<RepairJob> RepairJobs => Set<RepairJob>();

    // Facturation
    public DbSet<Invoice> Invoices => Set<Invoice>();

    // Caisse
    public DbSet<RegisterSession> RegisterSessions => Set<RegisterSession>();
    public DbSet<Sale> Sales => Set<Sale>();

    // Approvisionnement
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoredEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.StreamId, x.Version }).IsUnique();
        });

        modelBuilder.Entity<Product>().HasIndex(x => x.Sku).IsUnique();

        // Les lignes sont des objets-valeurs sans identité propre : stockées en JSON.
        modelBuilder.Entity<RepairJob>().OwnsMany(x => x.Parts, b => b.ToJson());
        modelBuilder.Entity<Invoice>().OwnsMany(x => x.Lines, b => b.ToJson());
        modelBuilder.Entity<Sale>().OwnsMany(x => x.Lines, b => b.ToJson());
        modelBuilder.Entity<PurchaseOrder>().OwnsMany(x => x.Lines, b => b.ToJson());
    }
}
