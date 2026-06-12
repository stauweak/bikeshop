using System.Net;
using System.Net.Http.Json;

namespace Bikeshop.Api.Tests;

// Pas de fixture partagée : une seule session de caisse peut être ouverte à la fois,
// chaque test démarre donc sur une base vierge.
public class PosTests : IDisposable
{
    private readonly TestAppFactory _factory = new();
    private readonly HttpClient _client;

    public PosTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private async Task<string> CreateProductAsync(string sku, decimal qty, decimal price = 10m)
    {
        var product = await _client.PostAndReadAsync("/api/inventory/products", new
        {
            sku, name = $"Article {sku}", category = "Accessory",
            unitPrice = price, unitCost = 5m, vatRate = 0.20m,
            quantityOnHand = qty, reorderThreshold = 1m,
        });
        return product.GetProperty("id").GetString()!;
    }

    private async Task<string> OpenSessionAsync(decimal openingFloat = 100m)
    {
        var session = await _client.PostAndReadAsync("/api/pos/sessions/open", new { openingFloat });
        return session.GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task Sale_requires_an_open_session()
    {
        var productId = await CreateProductAsync("SKU-NOSESS", 5);
        var response = await _client.PostAsJsonAsync("/api/pos/sales", new
        {
            paymentMethod = "Cash",
            lines = new[] { new { productId, quantity = 1m } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Full_register_session_lifecycle()
    {
        var productId = await CreateProductAsync("SKU-POS", qty: 10, price: 20m);
        var sessionId = await OpenSessionAsync(openingFloat: 50m);

        // une seule session ouverte à la fois
        var reopen = await _client.PostAsJsonAsync("/api/pos/sessions/open", new { openingFloat = 10m });
        Assert.Equal(HttpStatusCode.BadRequest, reopen.StatusCode);

        // vente en espèces : 2 × 20 € HT + TVA 20 % = 48 € TTC
        var sale = await _client.PostAndReadAsync("/api/pos/sales", new
        {
            paymentMethod = "Cash",
            lines = new[] { new { productId, quantity = 2m } },
        });
        Assert.Equal(48m, sale.GetProperty("totalInclVat").GetDecimal());

        // vente par carte : 1 × 20 € HT = 24 € TTC
        await _client.PostAndReadAsync("/api/pos/sales", new
        {
            paymentMethod = "Card",
            lines = new[] { new { productId, quantity = 1m } },
        });

        // le stock a été décrémenté
        var product = await _client.GetJsonAsync($"/api/inventory/products/{productId}");
        Assert.Equal(7m, product.GetProperty("quantityOnHand").GetDecimal());

        // les ventes sont passées en comptabilité : caisse 48, banque 24
        var balance = await _client.GetJsonAsync("/api/accounting/balance");
        Assert.Equal(48m, balance.EnumerateArray().Single(a => a.GetProperty("number").GetString() == "530").GetProperty("balance").GetDecimal());
        Assert.Equal(24m, balance.EnumerateArray().Single(a => a.GetProperty("number").GetString() == "512").GetProperty("balance").GetDecimal());

        // clôture avec 98 € comptés : 50 (fond) + 48 (espèces) = 98 attendu, écart 0
        var summary = await _client.PostAndReadAsync($"/api/pos/sessions/{sessionId}/close", new { countedCash = 98m });
        Assert.Equal(2, summary.GetProperty("saleCount").GetInt32());
        Assert.Equal(48m, summary.GetProperty("cashTotal").GetDecimal());
        Assert.Equal(24m, summary.GetProperty("cardTotal").GetDecimal());
        Assert.Equal(98m, summary.GetProperty("expectedCash").GetDecimal());
        Assert.Equal(0m, summary.GetProperty("cashDifference").GetDecimal());
    }

    [Fact]
    public async Task Sale_with_insufficient_stock_is_rejected()
    {
        var productId = await CreateProductAsync("SKU-EMPTY", qty: 0);
        await OpenSessionAsync();
        var response = await _client.PostAsJsonAsync("/api/pos/sales", new
        {
            paymentMethod = "Cash",
            lines = new[] { new { productId, quantity = 1m } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
