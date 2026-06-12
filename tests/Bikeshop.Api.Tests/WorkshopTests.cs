using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bikeshop.Api.Tests;

public class WorkshopTests : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client;

    public WorkshopTests(TestAppFactory factory) => _client = factory.CreateClient();

    private async Task<string> CreateMechanicAsync(string name = "Jean Réparation")
    {
        var mechanic = await _client.PostAndReadAsync("/api/workshop/mechanics", new { name });
        return mechanic.GetProperty("id").GetString()!;
    }

    private async Task<string> CreateProductAsync(string sku, decimal qty)
    {
        var product = await _client.PostAndReadAsync("/api/inventory/products", new
        {
            sku, name = $"Pièce {sku}", category = "Component",
            unitPrice = 20m, unitCost = 10m, vatRate = 0.20m,
            quantityOnHand = qty, reorderThreshold = 1m,
        });
        return product.GetProperty("id").GetString()!;
    }

    private async Task<JsonElement> CreateJobAsync(string mechanicId, object[]? parts = null) =>
        await _client.PostAndReadAsync("/api/workshop/jobs", new
        {
            customerName = "Alice Martin",
            bikeDescription = "VTT Rockrider",
            mechanicId,
            start = "2026-06-15T09:00:00Z",
            end = "2026-06-15T11:00:00Z",
            laborHours = 1.5m,
            parts,
        });

    [Fact]
    public async Task Job_appears_in_planning_for_its_date_range()
    {
        var mechanicId = await CreateMechanicAsync();
        var job = await CreateJobAsync(mechanicId);
        var jobId = job.GetProperty("id").GetString();

        var planning = await _client.GetJsonAsync("/api/workshop/planning?from=2026-06-15&to=2026-06-16");
        Assert.Contains(planning.EnumerateArray(), j => j.GetProperty("id").GetString() == jobId);

        var emptyPlanning = await _client.GetJsonAsync("/api/workshop/planning?from=2026-07-01&to=2026-07-02");
        Assert.DoesNotContain(emptyPlanning.EnumerateArray(), j => j.GetProperty("id").GetString() == jobId);
    }

    [Fact]
    public async Task Job_with_unknown_mechanic_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/workshop/jobs", new
        {
            customerName = "Bob",
            bikeDescription = "Vélo de course",
            mechanicId = Guid.NewGuid(),
            start = "2026-06-15T09:00:00Z",
            end = "2026-06-15T10:00:00Z",
            laborHours = 1m,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Completing_a_job_consumes_parts_from_stock()
    {
        var mechanicId = await CreateMechanicAsync("Marc Outils");
        var productId = await CreateProductAsync("SKU-WSP", qty: 10);
        var job = await CreateJobAsync(mechanicId, [new { productId, quantity = 2m }]);
        var jobId = job.GetProperty("id").GetString();

        var completed = await _client.PostAndReadAsync($"/api/workshop/jobs/{jobId}/complete", new { });
        Assert.Equal("Completed", completed.GetProperty("status").GetString());

        var product = await _client.GetJsonAsync($"/api/inventory/products/{productId}");
        Assert.Equal(8m, product.GetProperty("quantityOnHand").GetDecimal());
    }

    [Fact]
    public async Task Invoicing_a_completed_job_creates_an_invoice_with_labor_and_parts()
    {
        var mechanicId = await CreateMechanicAsync("Léa Vélo");
        var productId = await CreateProductAsync("SKU-WSI", qty: 10);
        var job = await CreateJobAsync(mechanicId, [new { productId, quantity = 1m }]);
        var jobId = job.GetProperty("id").GetString();

        await _client.PostAndReadAsync($"/api/workshop/jobs/{jobId}/complete", new { });
        var result = await _client.PostAndReadAsync($"/api/workshop/jobs/{jobId}/invoice", new { });
        var invoiceId = result.GetProperty("invoiceId").GetString();

        var invoice = await _client.GetJsonAsync($"/api/invoicing/invoices/{invoiceId}");
        Assert.Equal(2, invoice.GetProperty("lines").GetArrayLength()); // main-d'œuvre + 1 pièce
        // 1,5 h × 45 € + 1 × 20 € = 87,50 € HT
        Assert.Equal(87.5m, invoice.GetProperty("totalExclVat").GetDecimal());

        var updatedJob = await _client.GetJsonAsync($"/api/workshop/jobs/{jobId}");
        Assert.Equal("Invoiced", updatedJob.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Invoicing_a_scheduled_job_is_rejected()
    {
        var mechanicId = await CreateMechanicAsync("Tom Pignon");
        var job = await CreateJobAsync(mechanicId);
        var response = await _client.PostAsJsonAsync($"/api/workshop/jobs/{job.GetProperty("id").GetString()}/invoice", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
