using Bogus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Data;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole());

var connectionString = configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=portfolio_thermometer;Username=postgres;Password=postgres";

services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

await using var sp = services.BuildServiceProvider();
var db = sp.GetRequiredService<AppDbContext>();
var logger = sp.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Applying migrations…");
await db.Database.MigrateAsync();

var customerCount = await db.Customers.CountAsync();
if (customerCount > 0)
{
    logger.LogInformation("Database already seeded with {Count} customers. Skipping.", customerCount);
    return;
}

logger.LogInformation("Seeding database…");

Randomizer.Seed = new Random(42);

var segments = new[] { "enterprise", "mid-market", "smb" };
var accountManagers = new[] { "Alice Müller", "Bob Schmidt", "Carol Wagner", "David Fischer", "Eva Weber" };

// ── Customers ─────────────────────────────────────────────────────────────────
var customerFaker = new Faker<Customer>()
    .RuleFor(c => c.Id, _ => Guid.NewGuid())
    .RuleFor(c => c.CrmExternalId, f => $"CRM-{f.Random.Number(1000, 9999)}")
    .RuleFor(c => c.Name, f => f.Name.FullName())
    .RuleFor(c => c.CompanyName, f => f.Company.CompanyName())
    .RuleFor(c => c.Email, f => f.Internet.Email())
    .RuleFor(c => c.Phone, f => f.Phone.PhoneNumber())
    .RuleFor(c => c.Segment, f => f.PickRandom(segments))
    .RuleFor(c => c.AccountManager, f => f.PickRandom(accountManagers))
    .RuleFor(c => c.OnboardingDate, f => DateOnly.FromDateTime(f.Date.Past(3)))
    .RuleFor(c => c.IsActive, f => f.Random.Bool(0.9f))
    .RuleFor(c => c.ImportedAt, _ => DateTimeOffset.UtcNow)
    .RuleFor(c => c.UpdatedAt, _ => DateTimeOffset.UtcNow);

var customers = customerFaker.Generate(80);
db.Customers.AddRange(customers);
await db.SaveChangesAsync();
logger.LogInformation("Seeded {Count} customers", customers.Count);

// ── Contracts ─────────────────────────────────────────────────────────────────
var contractTypes = new[] { "subscription", "one-time", "retainer" };
var contractStatuses = new[] { "active", "expired", "cancelled" };
var contracts = new List<Contract>();
var contractFaker = new Faker();

foreach (var customer in customers)
{
    var count = contractFaker.Random.Number(1, 3);
    for (var i = 0; i < count; i++)
    {
        var start = DateOnly.FromDateTime(contractFaker.Date.Past(2));
        var duration = contractFaker.Random.Number(3, 24);
        var end = start.AddMonths(duration);
        var status = end < DateOnly.FromDateTime(DateTime.UtcNow) ? "expired" : "active";

        contracts.Add(new Contract
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            CrmExternalId = $"CON-{contractFaker.Random.Number(10000, 99999)}",
            ContractType = contractFaker.PickRandom(contractTypes),
            StartDate = start,
            EndDate = end,
            MonthlyValue = contractFaker.Finance.Amount(500, 15000),
            Currency = "EUR",
            Status = status,
            AutoRenew = contractFaker.Random.Bool(0.4f),
            ImportedAt = DateTimeOffset.UtcNow
        });
    }
}

db.Contracts.AddRange(contracts);
await db.SaveChangesAsync();
logger.LogInformation("Seeded {Count} contracts", contracts.Count);

// ── Invoices ──────────────────────────────────────────────────────────────────
var invoiceStatuses = new[] { "paid", "unpaid", "overdue", "partial" };
var invoices = new List<Invoice>();
var invoiceFaker = new Faker();

foreach (var customer in customers)
{
    var count = invoiceFaker.Random.Number(2, 8);
    for (var i = 0; i < count; i++)
    {
        var issued = DateOnly.FromDateTime(invoiceFaker.Date.Past(1));
        var due = issued.AddDays(30);

        invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            CrmExternalId = $"INV-{invoiceFaker.Random.Number(10000, 99999)}",
            InvoiceNumber = $"INV-{invoiceFaker.Random.Number(1000, 9999)}",
            IssuedDate = issued,
            DueDate = due,
            Amount = invoiceFaker.Finance.Amount(200, 20000),
            Currency = "EUR",
            Status = invoiceFaker.PickRandom(invoiceStatuses),
            ImportedAt = DateTimeOffset.UtcNow
        });
    }
}

db.Invoices.AddRange(invoices);
await db.SaveChangesAsync();
logger.LogInformation("Seeded {Count} invoices", invoices.Count);

// ── Payments ──────────────────────────────────────────────────────────────────
var payments = new List<Payment>();
var paymentFaker = new Faker();

foreach (var invoice in invoices.Where(i => i.Status == "paid" || i.Status == "partial"))
{
    var daysLate = paymentFaker.Random.Number(-10, 60);
    var paymentDate = invoice.DueDate!.Value.AddDays(daysLate);

    payments.Add(new Payment
    {
        Id = Guid.NewGuid(),
        InvoiceId = invoice.Id,
        CustomerId = invoice.CustomerId,
        CrmExternalId = $"PAY-{paymentFaker.Random.Number(10000, 99999)}",
        PaymentDate = paymentDate,
        Amount = invoice.Status == "partial" ? invoice.Amount * 0.5m : invoice.Amount,
        DaysLate = daysLate,
        ImportedAt = DateTimeOffset.UtcNow
    });
}

db.Payments.AddRange(payments);
await db.SaveChangesAsync();
logger.LogInformation("Seeded {Count} payments", payments.Count);

// ── Complaints ────────────────────────────────────────────────────────────────
var categories = new[] { "billing", "service", "product", "other" };
var severities = new[] { "low", "medium", "high", "critical" };
var complaints = new List<Complaint>();
var complaintFaker = new Faker();

foreach (var customer in customers.Where(_ => complaintFaker.Random.Bool(0.4f)))
{
    var count = complaintFaker.Random.Number(1, 4);
    for (var i = 0; i < count; i++)
    {
        var created = DateOnly.FromDateTime(complaintFaker.Date.Past(1));
        var resolved = complaintFaker.Random.Bool(0.7f) ? created.AddDays(complaintFaker.Random.Number(1, 30)) : (DateOnly?)null;

        complaints.Add(new Complaint
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            CrmExternalId = $"CMP-{complaintFaker.Random.Number(10000, 99999)}",
            CreatedDate = created,
            ResolvedDate = resolved,
            Category = complaintFaker.PickRandom(categories),
            Severity = complaintFaker.PickRandom(severities),
            Description = complaintFaker.Lorem.Sentence(),
            ImportedAt = DateTimeOffset.UtcNow
        });
    }
}

db.Complaints.AddRange(complaints);
await db.SaveChangesAsync();
logger.LogInformation("Seeded {Count} complaints", complaints.Count);

// ── Interactions ──────────────────────────────────────────────────────────────
var channels = new[] { "email", "phone", "meeting", "chat" };
var directions = new[] { "inbound", "outbound" };
var sentiments = new[] { "positive", "neutral", "negative" };
var interactions = new List<Interaction>();
var interactionFaker = new Faker();

foreach (var customer in customers)
{
    var count = interactionFaker.Random.Number(2, 12);
    for (var i = 0; i < count; i++)
    {
        interactions.Add(new Interaction
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            CrmExternalId = $"INT-{interactionFaker.Random.Number(10000, 99999)}",
            InteractionDate = DateOnly.FromDateTime(interactionFaker.Date.Past(1)),
            Channel = interactionFaker.PickRandom(channels),
            Direction = interactionFaker.PickRandom(directions),
            Summary = interactionFaker.Lorem.Sentence(),
            Sentiment = interactionFaker.Random.Bool(0.8f) ? interactionFaker.PickRandom(sentiments) : null,
            ImportedAt = DateTimeOffset.UtcNow
        });
    }
}

db.Interactions.AddRange(interactions);
await db.SaveChangesAsync();
logger.LogInformation("Seeded {Count} interactions", interactions.Count);

logger.LogInformation("Seeding complete.");
