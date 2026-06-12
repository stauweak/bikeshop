using System.Net;
using System.Net.Http.Json;

namespace Bikeshop.Api.Tests;

public class InventoryTests : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client;

    public InventoryTests(TestAppFactory factory) => _client = factory.CreateClient();

    private Task<System.Text.Json.JsonElement> CreateProductAsync(string sku, decimal qty = 10, decimal threshold = 2) =>
        _client.PostAndReadAsync("/api/inventory/products", new
        {
            sku,
            name = $"Produit {sku}",
            category = "Component",
            unitPrice = 25m,
            unitCost = 12m,
            vatRate = 0.20m,
            quantityOnHand = qty,
            reorderThreshold = threshold,
        });

    [Fact]
    public async Task Product_crud_works()
    {
        var created = await CreateProductAsync("SKU-CRUD");
        var id = created.GetProperty("id").GetString();

        var fetched = await _client.GetJsonAsync($"/api/inventory/products/{id}");
        Assert.Equal("Produit SKU-CRUD", fetched.GetProperty("name").GetString());

        var updateResponse = await _client.PutAsJsonAsync($"/api/inventory/products/{id}", new
        {
            sku = "SKU-CRUD",
            name = "Chaîne 11V",
            category = "Component",
            unitPrice = 30m,
            unitCost = 14m,
            vatRate = 0.20m,
            quantityOnHand = 10m,
            reorderThreshold = 3m,
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await _client.GetJsonAsync($"/api/inventory/products/{id}");
        Assert.Equal("Chaîne 11V", updated.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Duplicate_sku_is_rejected()
    {
        await CreateProductAsync("SKU-DUP");
        var response = await _client.PostAsJsonAsync("/api/inventory/products", new
        {
            sku = "SKU-DUP", name = "Doublon", category = "Component",
            unitPrice = 1m, unitCost = 1m, vatRate = 0.20m, quantityOnHand = 0m, reorderThreshold = 0m,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Adjustment_moves_stock_and_records_movement()
    {
        var created = await CreateProductAsync("SKU-ADJ", qty: 5);
        var id = created.GetProperty("id").GetString();

        var adjusted = await _client.PostAndReadAsync($"/api/inventory/products/{id}/adjust",
            new { quantity = -2m, reason = "Adjustment", reference = "Inventaire" });
        Assert.Equal(3m, adjusted.GetProperty("quantityOnHand").GetDecimal());

        var movements = await _client.GetJsonAsync("/api/inventory/movements");
        Assert.Contains(movements.EnumerateArray(),
            m => m.GetProperty("productId").GetString() == id && m.GetProperty("quantity").GetDecimal() == -2m);
    }

    [Fact]
    public async Task Stock_cannot_go_negative()
    {
        var created = await CreateProductAsync("SKU-NEG", qty: 1);
        var id = created.GetProperty("id").GetString();

        var response = await _client.PostAsJsonAsync($"/api/inventory/products/{id}/adjust",
            new { quantity = -5m, reason = "Adjustment", reference = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Low_stock_lists_products_under_threshold()
    {
        var created = await CreateProductAsync("SKU-LOW", qty: 1, threshold: 5);
        var id = created.GetProperty("id").GetString();

        var lowStock = await _client.GetJsonAsync("/api/inventory/low-stock");
        Assert.Contains(lowStock.EnumerateArray(), p => p.GetProperty("id").GetString() == id);
    }
}
