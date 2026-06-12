using System.Net;
using System.Net.Http.Json;

namespace Bikeshop.Api.Tests;

public class AccountingTests : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client;

    public AccountingTests(TestAppFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Chart_of_accounts_is_seeded()
    {
        var accounts = await _client.GetJsonAsync("/api/accounting/accounts");
        var numbers = accounts.EnumerateArray().Select(a => a.GetProperty("number").GetString()).ToList();
        Assert.Contains("512", numbers);
        Assert.Contains("530", numbers);
        Assert.Contains("707", numbers);
        Assert.Contains("44571", numbers);
    }

    [Fact]
    public async Task Posting_a_balanced_entry_updates_balance_and_journal()
    {
        await _client.PostAndReadAsync("/api/accounting/entries", new
        {
            date = "2026-06-01",
            label = "Apport en banque",
            lines = new[]
            {
                new { account = "512", debit = 1000m, credit = 0m },
                new { account = "707", debit = 0m, credit = 1000m },
            },
        });

        var journal = await _client.GetJsonAsync("/api/accounting/journal");
        Assert.Contains(journal.EnumerateArray(), e => e.GetProperty("label").GetString() == "Apport en banque");

        var balance = await _client.GetJsonAsync("/api/accounting/balance");
        var bank = balance.EnumerateArray().Single(a => a.GetProperty("number").GetString() == "512");
        Assert.Equal(1000m, bank.GetProperty("balance").GetDecimal());

        var ledger = await _client.GetJsonAsync("/api/accounting/accounts/512/ledger");
        Assert.Equal(1000m, ledger.EnumerateArray().Last().GetProperty("runningBalance").GetDecimal());
    }

    [Fact]
    public async Task Unbalanced_entry_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/accounting/entries", new
        {
            date = "2026-06-01",
            label = "Écriture déséquilibrée",
            lines = new[]
            {
                new { account = "512", debit = 100m, credit = 0m },
                new { account = "707", debit = 0m, credit = 50m },
            },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Entry_on_unknown_account_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/accounting/entries", new
        {
            date = "2026-06-01",
            label = "Compte inconnu",
            lines = new[]
            {
                new { account = "999999", debit = 100m, credit = 0m },
                new { account = "707", debit = 0m, credit = 100m },
            },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Opening_an_account_twice_is_rejected()
    {
        var first = await _client.PostAsJsonAsync("/api/accounting/accounts",
            new { number = "601", name = "Achats matières", type = "Expense" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/api/accounting/accounts",
            new { number = "601", name = "Doublon", type = "Expense" });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Ledger_of_unknown_account_returns_404()
    {
        var response = await _client.GetAsync("/api/accounting/accounts/000000/ledger");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
