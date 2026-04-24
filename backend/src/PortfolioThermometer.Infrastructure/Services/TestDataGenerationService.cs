using PortfolioThermometer.Core.Interfaces;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Data;

namespace PortfolioThermometer.Infrastructure.Services;

public sealed class TestDataGenerationService(AppDbContext db) : ITestDataGenerationService
{
    private static readonly string[] CompanyPrefixes =
        ["Van", "De", "Den", "Het", "Groep", "NV", "BVBA", "VZW", "Coöp"];

    private static readonly string[] CompanyRoots =
        ["Energie", "Power", "Solar", "Stroom", "Gas", "Flex", "Smart", "Green",
         "Volt", "Lux", "Ster", "Wind", "Eco", "Net", "Grid", "Park", "Bio"];

    private static readonly string[] CompanySuffixes =
        ["Solutions", "Services", "Industries", "Group", "Partners", "Works",
         "Systems", "Beheer", "Exploitatie", "Invest", "Holding", "Connect"];

    private static readonly string[] Segments =
        ["SME", "corporate", "residential", "collective", "company"];

    private static readonly string[] AccountManagers =
        ["Sophie Martens", "Luc Desmet", "Nathalie Peeters", "Koen Janssen",
         "Emma Claes", "Pieter Van den Berg", "Julie Lemmens", "Thomas Wouters"];

    private static readonly string[] ComplaintCategories =
        ["billing", "technical", "service", "meter_reading", "contract", "pricing"];

    private static readonly string[] ComplaintSeverities = ["low", "medium", "high"];

    private static readonly string[] ComplaintDescriptions =
        ["Invoice amount incorrect", "Meter not accessible", "No response from support",
         "Reading not matching expected consumption", "Contract terms unclear",
         "Price increase not communicated", "Connection issue reported", "Billing dispute"];

    private static readonly string[] Channels = ["email", "phone", "chat", "visit"];
    private static readonly string[] Directions = ["inbound", "outbound"];
    private static readonly string[] Sentiments = ["positive", "neutral", "negative"];

    private static readonly string[] InteractionSummaries =
        ["Follow-up on last invoice", "Technical issue reported", "Contract renewal discussion",
         "Meter reading enquiry", "General account review", "Pricing query", "Support request",
         "Onboarding call", "Complaint follow-up", "Service interruption reported"];

    private static readonly string[] ProductTypes = ["Electricity", "Gas"];
    private static readonly string[] DeliveryTypes = ["LDN", "ODN", "NA"];

    private static readonly string[] ContractTypes =
        ["fixed", "variable", "dynamic", "green", "standard", "premium"];

    private static readonly string[] InvoiceStatuses =
        ["paid", "unpaid", "overdue", "partial"];

    public async Task<TestDataGenerationResult> GenerateAsync(int customerCount, CancellationToken ct = default)
    {
        var rng = new Random();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.DateTime);

        var customers = new List<Customer>(customerCount);
        var connections = new List<Connection>();
        var meterReads = new List<MeterRead>();
        var contracts = new List<Contract>();
        var invoices = new List<Invoice>();
        var payments = new List<Payment>();
        var complaints = new List<Complaint>();
        var interactions = new List<Interaction>();

        for (var i = 0; i < customerCount; i++)
        {
            var customerId = Guid.NewGuid();
            var externalId = $"TEST-{customerId:N}";
            var name = GenerateCompanyName(rng);
            var segment = Pick(rng, Segments);
            var onboardingDate = today.AddDays(-rng.Next(180, 2000));

            var customer = new Customer
            {
                Id = customerId,
                CrmExternalId = externalId,
                Name = name,
                CompanyName = name,
                Email = $"contact@{name.ToLowerInvariant().Replace(" ", "")}.be",
                Phone = $"+32{rng.Next(400_000_000, 499_999_999)}",
                Segment = segment,
                AccountManager = Pick(rng, AccountManagers),
                OnboardingDate = onboardingDate,
                IsActive = rng.NextDouble() > 0.05,
                ImportedAt = now,
                UpdatedAt = now,
            };
            customers.Add(customer);

            // Connections: 1–3 per customer
            var connectionCount = rng.Next(1, 4);
            var customerConnections = new List<Connection>(connectionCount);
            for (var c = 0; c < connectionCount; c++)
            {
                var conn = new Connection
                {
                    Id = Guid.NewGuid(),
                    CrmExternalId = $"TEST-CONN-{Guid.NewGuid():N}",
                    CustomerId = customerId,
                    Ean = GenerateEan(rng),
                    ProductType = Pick(rng, ProductTypes),
                    DeliveryType = Pick(rng, DeliveryTypes),
                    ConnectionTypeId = rng.Next(1, 5),
                    ImportedAt = now,
                };
                customerConnections.Add(conn);
                connections.Add(conn);
            }

            // MeterReads: monthly for the last 12–24 months per connection
            var monthCount = rng.Next(12, 25);
            var readEndUtc = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var readStartUtc = readEndUtc.AddMonths(-monthCount);

            foreach (var conn in customerConnections)
            {
                var unit = conn.ProductType == "Gas" ? "m3" : "kWh";
                var baseMonthly = conn.ProductType == "Gas"
                    ? rng.Next(50, 400)
                    : rng.Next(200, 2000);

                for (var m = readStartUtc; m < readEndUtc; m = m.AddMonths(1))
                {
                    var jitter = 0.85 + rng.NextDouble() * 0.30;
                    var consumption = Math.Round((decimal)(baseMonthly * jitter), 2);

                    meterReads.Add(new MeterRead
                    {
                        Id = Guid.NewGuid(),
                        CrmExternalId = $"TEST-READ-{conn.Id:N}-{m:yyyyMM}",
                        ConnectionId = conn.Id,
                        StartDate = m,
                        EndDate = m.AddMonths(1),
                        Consumption = consumption,
                        Unit = unit,
                        UsageType = "UsageHigh",
                        Direction = "Consumption",
                        Quality = rng.NextDouble() > 0.3 ? "Measured" : "Estimated",
                        Source = "TestDataGenerator",
                        ImportedAt = now,
                    });
                }
            }

            // Contracts: 1–2 per customer
            var contractCount = rng.Next(1, 3);
            for (var c = 0; c < contractCount; c++)
            {
                var startDate = onboardingDate.AddMonths(c * 12);
                var endDate = rng.NextDouble() > 0.3 ? startDate.AddMonths(12 + rng.Next(0, 13)) : (DateOnly?)null;
                contracts.Add(new Contract
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerId,
                    CrmExternalId = $"TEST-CONTRACT-{Guid.NewGuid():N}",
                    ContractType = Pick(rng, ContractTypes),
                    StartDate = startDate,
                    EndDate = endDate,
                    MonthlyValue = Math.Round((decimal)(rng.Next(50, 5000) + rng.NextDouble()), 2),
                    Currency = "EUR",
                    Status = endDate.HasValue && endDate.Value < today ? "expired" : "active",
                    AutoRenew = rng.NextDouble() > 0.5,
                    ImportedAt = now,
                });
            }

            // Invoices: monthly for the last 12 months
            for (var m = 1; m <= 12; m++)
            {
                var issuedDate = today.AddMonths(-m);
                var dueDate = issuedDate.AddDays(30);
                var amount = Math.Round((decimal)(rng.Next(50, 2000) + rng.NextDouble()), 2);

                string status;
                var rand = rng.NextDouble();
                if (rand < 0.65) status = "paid";
                else if (rand < 0.80) status = "unpaid";
                else if (rand < 0.92) status = "overdue";
                else status = "partial";

                var invoice = new Invoice
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerId,
                    CrmExternalId = $"TEST-INV-{Guid.NewGuid():N}",
                    InvoiceNumber = $"INV-TEST-{rng.Next(10000, 99999)}",
                    IssuedDate = issuedDate,
                    DueDate = dueDate,
                    Amount = amount,
                    Currency = "EUR",
                    Status = status,
                    ImportedAt = now,
                };
                invoices.Add(invoice);

                if (status is "paid" or "partial")
                {
                    var paidAmount = status == "paid" ? amount : Math.Round(amount * (decimal)(0.3 + rng.NextDouble() * 0.5), 2);
                    var daysLate = status == "paid" && rng.NextDouble() > 0.7 ? rng.Next(1, 45) : 0;
                    payments.Add(new Payment
                    {
                        Id = Guid.NewGuid(),
                        InvoiceId = invoice.Id,
                        CustomerId = customerId,
                        CrmExternalId = $"TEST-PAY-{Guid.NewGuid():N}",
                        PaymentDate = dueDate.AddDays(daysLate),
                        Amount = paidAmount,
                        DaysLate = daysLate,
                        ImportedAt = now,
                    });
                }
            }

            // Complaints: 0–4 per customer
            var complaintCount = rng.Next(0, 5);
            for (var c = 0; c < complaintCount; c++)
            {
                var createdDate = today.AddDays(-rng.Next(10, 400));
                var isResolved = rng.NextDouble() > 0.35;
                complaints.Add(new Complaint
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerId,
                    CrmExternalId = $"TEST-COMPL-{Guid.NewGuid():N}",
                    CreatedDate = createdDate,
                    ResolvedDate = isResolved ? createdDate.AddDays(rng.Next(1, 30)) : null,
                    Category = Pick(rng, ComplaintCategories),
                    Severity = Pick(rng, ComplaintSeverities),
                    Description = Pick(rng, ComplaintDescriptions),
                    ImportedAt = now,
                });
            }

            // Interactions: 2–10 per customer
            var interactionCount = rng.Next(2, 11);
            for (var c = 0; c < interactionCount; c++)
            {
                interactions.Add(new Interaction
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerId,
                    CrmExternalId = $"TEST-INT-{Guid.NewGuid():N}",
                    InteractionDate = today.AddDays(-rng.Next(1, 500)),
                    Channel = Pick(rng, Channels),
                    Direction = Pick(rng, Directions),
                    Summary = Pick(rng, InteractionSummaries),
                    Sentiment = Pick(rng, Sentiments),
                    ImportedAt = now,
                });
            }
        }

        await db.Customers.AddRangeAsync(customers, ct);
        await db.Connections.AddRangeAsync(connections, ct);
        await db.Contracts.AddRangeAsync(contracts, ct);
        await db.Invoices.AddRangeAsync(invoices, ct);
        await db.Payments.AddRangeAsync(payments, ct);
        await db.Complaints.AddRangeAsync(complaints, ct);
        await db.Interactions.AddRangeAsync(interactions, ct);
        await db.MeterReads.AddRangeAsync(meterReads, ct);
        await db.SaveChangesAsync(ct);

        return new TestDataGenerationResult(
            CustomersCreated: customers.Count,
            ConnectionsCreated: connections.Count,
            MeterReadsCreated: meterReads.Count,
            ContractsCreated: contracts.Count,
            InvoicesCreated: invoices.Count,
            PaymentsCreated: payments.Count,
            ComplaintsCreated: complaints.Count,
            InteractionsCreated: interactions.Count);
    }

    private static string GenerateCompanyName(Random rng)
    {
        var style = rng.Next(3);
        return style switch
        {
            0 => $"{Pick(rng, CompanyPrefixes)} {Pick(rng, CompanyRoots)} {Pick(rng, CompanySuffixes)}",
            1 => $"{Pick(rng, CompanyRoots)}{Pick(rng, CompanyRoots)} {Pick(rng, CompanySuffixes)}",
            _ => $"{Pick(rng, CompanyRoots)} {Pick(rng, CompanySuffixes)} {rng.Next(1, 999)}",
        };
    }

    private static string GenerateEan(Random rng)
    {
        // Belgian EAN starts with 541 (electricity) or 542 (gas), 18 digits total
        var prefix = rng.NextDouble() > 0.5 ? "541" : "542";
        return prefix + rng.NextInt64(100_000_000_000_000L, 999_999_999_999_999L).ToString();
    }

    private static T Pick<T>(Random rng, T[] array) => array[rng.Next(array.Length)];
}
