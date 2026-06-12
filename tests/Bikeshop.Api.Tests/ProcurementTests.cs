using System.Net;
using System.Net.Http.Json;

namespace Bikeshop.Api.Tests;

// Base vierge par test : les assertions portent sur des soldes comptables globaux.
public class ProcurementTests : IDisposable
{
    private readonly TestAppFactory _factory = new();
    private readonly HttpClient _client;

    public ProcurementTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private async Task<string> CreateSupplierAsync(string name = "Shimano France")
    {
        var supplier = await _client.PostAndReadAsync("/api/procurement/suppliers",
            new { name, email = "contact@fournisseur.fr", leadTimeDays = 5 });
        return supplier.GetProperty("id").GetString()!;
    }

    private async Task<string> CreateProductAsync(string sku, decimal qty, decimal threshold, string? supplierId = null)
    {
        var product = await _client.PostAndReadAsync("/api/inventory/products", new
        {
            sku, name = $"Composant {sku}", category = "Component",
            unitPrice = 30m, unitCost = 15m, vatRate = 0.20m,
            quantityOnHand = qty, reorderThreshold = threshold,
            preferredSupplierId = supplierId,
        });
        return product.GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task Order_lifecycle_receives_stock_and_posts_accounting_entry()
    {
        var supplierId = await CreateSupplierAsync();
        var productId = await CreateProductAsync("SKU-PO", qty: 2, threshold: 5);

        var order = await _client.PostAndReadAsync("/api/procurement/orders", new
        {
            supplierId,
            lines = new[] { new { productId, quantity = 10m, unitCost = 15m } },
        });
        var orderId = order.GetProperty("id").GetString();
        var number = order.GetProperty("number").GetString();
        Assert.Equal("Draft", order.GetProperty("status").GetString());
        Assert.Equal(150m, order.GetProperty("totalExclVat").GetDecimal());

        // réception impossible avant envoi
        var early = await _client.PostAsJsonAsync($"/api/procurement/orders/{orderId}/receive", new { });
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);

        await _client.PostAndReadAsync($"/api/procurement/orders/{orderId}/send", new { });
        var received = await _client.PostAndReadAsync($"/api/procurement/orders/{orderId}/receive", new { });
        Assert.Equal("Received", received.GetProperty("status").GetString());

        // stock incrémenté
        var product = await _client.GetJsonAsync($"/api/inventory/products/{productId}");
        Assert.Equal(12m, product.GetProperty("quantityOnHand").GetDecimal());

        // écriture comptable : 607 débité 150, 44566 débité 30, 401 crédité 180
        var journal = await _client.GetJsonAsync("/api/accounting/journal");
        Assert.Contains(journal.EnumerateArray(), e => e.GetProperty("reference").GetString() == number);
        var balance = await _client.GetJsonAsync("/api/accounting/balance");
        Assert.Equal(150m, balance.EnumerateArray().Single(a => a.GetProperty("number").GetString() == "607").GetProperty("balance").GetDecimal());
        Assert.Equal(-180m, balance.EnumerateArray().Single(a => a.GetProperty("number").GetString() == "401").GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task Replenishment_suggestions_include_low_stock_products()
    {
        var supplierId = await CreateSupplierAsync("SRAM Europe");
        var productId = await CreateProductAsync("SKU-REPL", qty: 1, threshold: 4, supplierId);

        var suggestions = await _client.GetJsonAsync("/api/procurement/replenishment-suggestions");
        var suggestion = suggestions.EnumerateArray().Single(s => s.GetProperty("id").GetString() == productId);
        Assert.Equal(7m, suggestion.GetProperty("suggestedQuantity").GetDecimal()); // 4×2 − 1
        Assert.Equal("SRAM Europe", suggestion.GetProperty("preferredSupplierName").GetString());
    }

    [Fact]
    public async Task Order_with_unknown_supplier_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/procurement/orders", new
        {
            supplierId = Guid.NewGuid(),
            lines = new[] { new { productId = Guid.NewGuid(), quantity = 1m } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Received_order_cannot_be_cancelled()
    {
        var supplierId = await CreateSupplierAsync("Mavic");
        var productId = await CreateProductAsync("SKU-CANCEL", qty: 0, threshold: 1);

        var order = await _client.PostAndReadAsync("/api/procurement/orders", new
        {
            supplierId,
            lines = new[] { new { productId, quantity = 3m, unitCost = 10m } },
        });
        var orderId = order.GetProperty("id").GetString();
        await _client.PostAndReadAsync($"/api/procurement/orders/{orderId}/send", new { });
        await _client.PostAndReadAsync($"/api/procurement/orders/{orderId}/receive", new { });

        var response = await _client.PostAsJsonAsync($"/api/procurement/orders/{orderId}/cancel", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
