namespace Bikeshop.Api.Modules.Accounting;

// ----- Événements du grand livre -----
public record AccountOpened(string Number, string Name, string Type);
public record JournalLine(string Account, decimal Debit, decimal Credit);
public record JournalEntryPosted(Guid EntryId, DateOnly Date, string Label, string? Reference, List<JournalLine> Lines);

// ----- Modèles de lecture (projections) -----
public record AccountView(string Number, string Name, string Type, decimal Debit, decimal Credit, decimal Balance);
public record EntryView(Guid EntryId, DateOnly Date, string Label, string? Reference, List<JournalLine> Lines);
public record LedgerLineView(DateOnly Date, string Label, string? Reference, decimal Debit, decimal Credit, decimal RunningBalance);

/// <summary>
/// Comptabilité en event sourcing : la seule écriture autorisée est l'ajout
/// d'un événement au flux "ledger" ; comptes, journal, balance et grands livres
/// sont des projections obtenues en rejouant ce flux.
/// </summary>
public class AccountingService(EventStore store)
{
    public const string StreamId = "ledger";

    private static readonly IReadOnlyDictionary<string, Type> EventTypes = new Dictionary<string, Type>
    {
        [nameof(AccountOpened)] = typeof(AccountOpened),
        [nameof(JournalEntryPosted)] = typeof(JournalEntryPosted),
    };

    // Comptes du plan comptable français utilisés par les autres modules.
    public static class Accounts
    {
        public const string Bank = "512";
        public const string CashBox = "530";
        public const string Customers = "411";
        public const string Suppliers = "401";
        public const string GoodsSales = "707";
        public const string ServiceSales = "706";
        public const string VatCollected = "44571";
        public const string VatDeductible = "44566";
        public const string GoodsPurchases = "607";
    }

    public async Task EnsureChartOfAccountsAsync()
    {
        if (await store.CountAsync(StreamId) > 0) return;

        (string Number, string Name, string Type)[] defaults =
        [
            (Accounts.Suppliers, "Fournisseurs", "Liability"),
            (Accounts.Customers, "Clients", "Asset"),
            (Accounts.VatDeductible, "TVA déductible", "Asset"),
            (Accounts.VatCollected, "TVA collectée", "Liability"),
            (Accounts.Bank, "Banque", "Asset"),
            (Accounts.CashBox, "Caisse", "Asset"),
            (Accounts.GoodsPurchases, "Achats de marchandises", "Expense"),
            (Accounts.ServiceSales, "Prestations de services (atelier)", "Revenue"),
            (Accounts.GoodsSales, "Ventes de marchandises", "Revenue"),
        ];
        foreach (var (number, name, type) in defaults)
            await store.AppendAsync(StreamId, new AccountOpened(number, name, type));
    }

    public async Task OpenAccountAsync(string number, string name, string type)
    {
        var state = await ReplayAsync();
        if (state.Accounts.ContainsKey(number))
            throw new InvalidOperationException($"Le compte {number} existe déjà.");
        await store.AppendAsync(StreamId, new AccountOpened(number, name, type));
    }

    public async Task<Guid> PostEntryAsync(DateOnly date, string label, string? reference, List<JournalLine> lines)
    {
        if (lines.Count < 2)
            throw new InvalidOperationException("Une écriture doit comporter au moins deux lignes.");
        if (lines.Any(l => l.Debit < 0 || l.Credit < 0 || (l.Debit > 0 && l.Credit > 0) || (l.Debit == 0 && l.Credit == 0)))
            throw new InvalidOperationException("Chaque ligne doit porter soit un débit, soit un crédit, strictement positif.");
        if (lines.Sum(l => l.Debit) != lines.Sum(l => l.Credit))
            throw new InvalidOperationException("L'écriture n'est pas équilibrée (total débits ≠ total crédits).");

        var state = await ReplayAsync();
        var unknown = lines.Select(l => l.Account).FirstOrDefault(a => !state.Accounts.ContainsKey(a));
        if (unknown is not null)
            throw new InvalidOperationException($"Compte inconnu : {unknown}.");

        var entry = new JournalEntryPosted(Guid.NewGuid(), date, label, reference, lines);
        await store.AppendAsync(StreamId, entry);
        return entry.EntryId;
    }

    public async Task<List<AccountView>> GetTrialBalanceAsync()
    {
        var state = await ReplayAsync();
        return state.Accounts.Values
            .OrderBy(a => a.Number)
            .Select(a => ToView(a, state))
            .ToList();
    }

    public async Task<List<EntryView>> GetJournalAsync()
    {
        var state = await ReplayAsync();
        return state.Entries
            .Select(e => new EntryView(e.EntryId, e.Date, e.Label, e.Reference, e.Lines))
            .ToList();
    }

    public async Task<List<LedgerLineView>?> GetLedgerAsync(string accountNumber)
    {
        var state = await ReplayAsync();
        if (!state.Accounts.ContainsKey(accountNumber)) return null;

        var result = new List<LedgerLineView>();
        decimal running = 0;
        foreach (var entry in state.Entries)
        {
            foreach (var line in entry.Lines.Where(l => l.Account == accountNumber))
            {
                running += line.Debit - line.Credit;
                result.Add(new LedgerLineView(entry.Date, entry.Label, entry.Reference, line.Debit, line.Credit, running));
            }
        }
        return result;
    }

    private static AccountView ToView((string Number, string Name, string Type) account, LedgerState state)
    {
        var debit = state.Entries.SelectMany(e => e.Lines).Where(l => l.Account == account.Number).Sum(l => l.Debit);
        var credit = state.Entries.SelectMany(e => e.Lines).Where(l => l.Account == account.Number).Sum(l => l.Credit);
        return new AccountView(account.Number, account.Name, account.Type, debit, credit, debit - credit);
    }

    private record LedgerState(
        Dictionary<string, (string Number, string Name, string Type)> Accounts,
        List<JournalEntryPosted> Entries);

    private async Task<LedgerState> ReplayAsync()
    {
        var accounts = new Dictionary<string, (string, string, string)>();
        var entries = new List<JournalEntryPosted>();
        foreach (var @event in await store.ReadStreamAsync(StreamId, EventTypes))
        {
            switch (@event)
            {
                case AccountOpened a:
                    accounts[a.Number] = (a.Number, a.Name, a.Type);
                    break;
                case JournalEntryPosted e:
                    entries.Add(e);
                    break;
            }
        }
        return new LedgerState(accounts, entries);
    }
}
