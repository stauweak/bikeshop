using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bikeshop.Api.Tests;

public class InvoicingTests : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client;

    public InvoicingTests(TestAppFactory factory) => _client = factory.CreateClient();

    private Task<JsonElement> CreateInvoiceAsync() =>
        _client.PostAndReadAsync("/api/invoicing/invoices", new
        {
            customerName = "Carole Dupont",
            issueDate = "2026-06-10",
            lines = new[]
            {
                new { description = "Révision complète", quantity = 1m, unitPrice = 80m, vatRate = 0.20m, isService = true },
                new { description = "Câble de frein", quantity = 2m, unitPrice = 5m, vatRate = 0.20m, isService = false },
            },
        });

    [Fact]
    public async Task Invoice_totals_are_computed()
    {
        var invoice = await CreateInvoiceAsync();
        Assert.Equal(90m, invoice.GetProperty("totalExclVat").GetDecimal());
        Assert.Equal(18m, invoice.GetProperty("totalVat").GetDecimal());
        Assert.Equal(108m, invoice.GetProperty("totalInclVat").GetDecimal());
        Assert.Equal("Draft", invoice.GetProperty("status").GetString());
        Assert.StartsWith("FAC-2026-", invoice.GetProperty("number").GetString());
    }

    [Fact]
    public async Task Issue_then_pay_posts_accounting_entries()
    {
        var invoice = await CreateInvoiceAsync();
        var id = invoice.GetProperty("id").GetString();
        var number = invoice.GetProperty("number").GetString();

        var issued = await _client.PostAndReadAsync($"/api/invoicing/invoices/{id}/issue", new { });
        Assert.Equal("Issued", issued.GetProperty("status").GetString());

        var paid = await _client.PostAndReadAsync($"/api/invoicing/invoices/{id}/pay",
            new { paymentMethod = "card", paymentDate = "2026-06-11" });
        Assert.Equal("Paid", paid.GetProperty("status").GetString());

        var journal = await _client.GetJsonAsync("/api/accounting/journal");
        var entries = journal.EnumerateArray()
            .Where(e => e.GetProperty("reference").GetString() == number)
            .ToList();
        Assert.Equal(2, entries.Count); // émission + règlement

        // après règlement par carte : 512 débité de 108, 411 soldé
        var balance = await _client.GetJsonAsync("/api/accounting/balance");
        var bank = balance.EnumerateArray().Single(a => a.GetProperty("number").GetString() == "512");
        var customers = balance.EnumerateArray().Single(a => a.GetProperty("number").GetString() == "411");
        Assert.Equal(108m, bank.GetProperty("balance").GetDecimal());
        Assert.Equal(0m, customers.GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task Paying_a_draft_invoice_is_rejected()
    {
        var invoice = await CreateInvoiceAsync();
        var response = await _client.PostAsJsonAsync(
            $"/api/invoicing/invoices/{invoice.GetProperty("id").GetString()}/pay",
            new { paymentMethod = "cash" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Empty_invoice_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/invoicing/invoices",
            new { customerName = "Vide", lines = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
