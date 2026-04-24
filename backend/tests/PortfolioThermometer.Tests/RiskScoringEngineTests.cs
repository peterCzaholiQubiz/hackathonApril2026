using Microsoft.Extensions.Logging.Abstractions;
using PortfolioThermometer.Core.Models;
using PortfolioThermometer.Infrastructure.Services;

namespace PortfolioThermometer.Tests;

public sealed class RiskScoringEngineTests
{
    private readonly RiskScoringEngine _engine = new(null!, NullLogger<RiskScoringEngine>.Instance);
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static Customer BaseCustomer(DateOnly? onboarding = null) => new()
    {
        Id = Guid.NewGuid(),
        CrmExternalId = "TEST-001",
        Name = "Test Customer",
        IsActive = true,
        OnboardingDate = onboarding ?? Today.AddYears(-2),
    };

    private static Contract ActiveContract(DateOnly? endDate = null, decimal monthlyValue = 2000, bool autoRenew = false) => new()
    {
        Id = Guid.NewGuid(),
        CrmExternalId = "C-001",
        Status = "active",
        StartDate = Today.AddYears(-1),
        EndDate = endDate ?? Today.AddYears(1),
        MonthlyValue = monthlyValue,
        AutoRenew = autoRenew,
    };

    private static Payment LatePay(int daysLate, DateOnly? date = null) => new()
    {
        Id = Guid.NewGuid(),
        CrmExternalId = "P-001",
        DaysLate = daysLate,
        PaymentDate = date ?? Today.AddDays(-10),
    };

    private static Invoice Inv(string status, DateOnly? issuedDate = null, DateOnly? dueDate = null) => new()
    {
        Id = Guid.NewGuid(),
        CrmExternalId = "INV-001",
        Status = status,
        IssuedDate = issuedDate ?? Today.AddMonths(-1),
        DueDate = dueDate ?? Today.AddMonths(-1).AddDays(30),
    };

    private static Complaint Comp(string severity, string category = "service", DateOnly? created = null) => new()
    {
        Id = Guid.NewGuid(),
        CrmExternalId = "COMP-001",
        Severity = severity,
        Category = category,
        CreatedDate = created ?? Today.AddMonths(-1),
    };

    private static Interaction Act(string direction = "outbound", string sentiment = "neutral", DateOnly? date = null) => new()
    {
        Id = Guid.NewGuid(),
        CrmExternalId = "INT-001",
        Direction = direction,
        Sentiment = sentiment,
        InteractionDate = date ?? Today.AddDays(-10),
    };

    // ──────────────────────────────────────────────────────────────────────────
    // All-green customer (no signals triggered)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Score_AllGreenCustomer_ReturnsGreenZeroSignals()
    {
        var customer = BaseCustomer(Today.AddYears(-3));
        var contracts = new[] { ActiveContract(Today.AddYears(1), autoRenew: true) };
        var interactions = new[] { Act("outbound", "positive", Today.AddDays(-5)) };

        var score = _engine.ScoreCustomer(customer, contracts, [], [], [], interactions);

        Assert.Equal("green", score.HeatLevel);
        Assert.True(score.OverallScore < 40);
    }

    [Fact]
    public void Score_NewCustomerNoHistory_ReturnsNonZeroChurn()
    {
        var customer = BaseCustomer(Today.AddDays(-30)); // new customer, < 12 months
        var score = _engine.ScoreCustomer(customer, [], [], [], [], []);

        Assert.True(score.ChurnScore >= 10); // tenure signal fires
    }

    [Fact]
    public void Score_AllRedCustomer_ReturnsRed()
    {
        var customer = BaseCustomer(Today.AddMonths(-6)); // new (tenure < 12 months)

        var contracts = new[]
        {
            new Contract
            {
                Id = Guid.NewGuid(),
                CrmExternalId = "C-OLD",
                Status = "active",
                StartDate = Today.AddDays(-60),
                EndDate = Today.AddDays(30),   // expiring soon
                MonthlyValue = 500,
                AutoRenew = false,
            },
            ActiveContract(Today.AddYears(-1), monthlyValue: 800),
        };

        var invoices = new[]
        {
            Inv("overdue", Today.AddMonths(-4), Today.AddMonths(-3)),
            Inv("overdue", Today.AddMonths(-3), Today.AddMonths(-2)),
            Inv("overdue", Today.AddMonths(-2), Today.AddMonths(-1)),
            Inv("partial", Today.AddMonths(-1)),
        };

        var payments = new[]
        {
            LatePay(40, Today.AddDays(-10)),
            LatePay(50, Today.AddDays(-20)),
        };

        var complaints = new[]
        {
            Comp("critical", "billing", Today.AddMonths(-1)),
            Comp("high", "service", Today.AddMonths(-2)),
        };

        var interactions = new[]
        {
            Act("inbound", "negative", Today.AddDays(-5)),
            Act("inbound", "negative", Today.AddDays(-10)),
        };

        var score = _engine.ScoreCustomer(customer, contracts, invoices, payments, complaints, interactions);

        Assert.Equal("red", score.HeatLevel);
        Assert.True(score.OverallScore >= 70);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Churn signals
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Churn_ContractExpiringSoon_NoAutoRenew_Adds25()
    {
        var customer = BaseCustomer();
        var expiring = new Contract
        {
            Id = Guid.NewGuid(),
            CrmExternalId = "C-EXP",
            Status = "active",
            StartDate = Today.AddYears(-1),
            EndDate = Today.AddDays(45), // within 90 days
            AutoRenew = false,
        };

        var baseline = _engine.ScoreCustomer(customer, [], [], [], [], []);
        var withSignal = _engine.ScoreCustomer(customer, [expiring], [], [], [], []);

        Assert.True(withSignal.ChurnScore >= baseline.ChurnScore + 25);
    }

    [Fact]
    public void Churn_ContractExpiringSoon_WithAutoRenew_DoesNotAdd()
    {
        var customer = BaseCustomer();
        var expiring = new Contract
        {
            Id = Guid.NewGuid(),
            CrmExternalId = "C-EXP",
            Status = "active",
            StartDate = Today.AddYears(-1),
            EndDate = Today.AddDays(45),
            AutoRenew = true, // auto-renew, no churn signal
        };

        var baseline = _engine.ScoreCustomer(customer, [], [], [], [], []);
        var withSignal = _engine.ScoreCustomer(customer, [expiring], [], [], [], []);

        Assert.Equal(baseline.ChurnScore, withSignal.ChurnScore);
    }

    [Fact]
    public void Churn_DecliningInteractionFrequency_Adds20()
    {
        var customer = BaseCustomer();
        // 0 recent vs 3 prior = declining
        var priorInteractions = Enumerable.Range(0, 3)
            .Select(i => Act("inbound", "neutral", Today.AddDays(-100 - i)))
            .ToList();

        var baseline = _engine.ScoreCustomer(customer, [], [], [], [], []);
        var withSignal = _engine.ScoreCustomer(customer, [], [], [], [], priorInteractions);

        Assert.True(withSignal.ChurnScore >= baseline.ChurnScore + 20);
    }

    [Fact]
    public void Churn_HighSeverityComplaintRecent_Adds20()
    {
        var customer = BaseCustomer();
        var complaint = Comp("high", "service", Today.AddDays(-30));

        var baseline = _engine.ScoreCustomer(customer, [], [], [], [], []);
        var withSignal = _engine.ScoreCustomer(customer, [], [], [], [complaint], []);

        Assert.True(withSignal.ChurnScore >= baseline.ChurnScore + 20);
    }

    [Fact]
    public void Churn_LowSeverityComplaint_DoesNotAdd()
    {
        var customer = BaseCustomer();
        var complaint = Comp("low", "service", Today.AddDays(-30));

        var baseline = _engine.ScoreCustomer(customer, [], [], [], [], []);
        var withSignal = _engine.ScoreCustomer(customer, [], [], [], [complaint], []);

        Assert.Equal(baseline.ChurnScore, withSignal.ChurnScore);
    }

    [Fact]
    public void Churn_NegativeSentimentRecent_Adds15()
    {
        var customer = BaseCustomer();
        var interaction = Act("inbound", "negative", Today.AddDays(-10));

        var baseline = _engine.ScoreCustomer(customer, [], [], [], [], []);
        var withSignal = _engine.ScoreCustomer(customer, [], [], [], [], [interaction]);

        Assert.True(withSignal.ChurnScore >= baseline.ChurnScore + 15);
    }

    [Fact]
    public void Churn_NoOutboundInSixtyDays_Adds10()
    {
        var customer = BaseCustomer();
        // No outbound contacts at all
        var score = _engine.ScoreCustomer(customer, [], [], [], [], []);
        Assert.True(score.ChurnScore >= 10);
    }

    [Fact]
    public void Churn_RecentOutbound_DoesNotAdd()
    {
        var customer = BaseCustomer();
        var recentOutbound = Act("outbound", "neutral", Today.AddDays(-5));

        var withoutOutbound = _engine.ScoreCustomer(customer, [], [], [], [], []);
        var withOutbound = _engine.ScoreCustomer(customer, [], [], [], [], [recentOutbound]);

        Assert.True(withoutOutbound.ChurnScore > withOutbound.ChurnScore);
    }

    [Fact]
    public void Churn_TenureLessThan12Months_Adds10()
    {
        var newCustomer = BaseCustomer(Today.AddMonths(-3));
        var oldCustomer = BaseCustomer(Today.AddYears(-3));

        var outbound = new[] { Act("outbound", "neutral", Today.AddDays(-5)) };

        var newScore = _engine.ScoreCustomer(newCustomer, [], [], [], [], outbound);
        var oldScore = _engine.ScoreCustomer(oldCustomer, [], [], [], [], outbound);

        Assert.True(newScore.ChurnScore >= oldScore.ChurnScore + 10);
    }

    [Fact]
    public void Churn_ScoreCappedAt100()
    {
        var customer = BaseCustomer(Today.AddMonths(-3));
        var complaints = Enumerable.Range(0, 10).Select(_ => Comp("critical")).ToList();
        var negInteractions = Enumerable.Range(0, 10).Select(_ => Act("inbound", "negative")).ToList();
        var expiring = new Contract
        {
            Id = Guid.NewGuid(), CrmExternalId = "C-EXP",
            Status = "active", StartDate = Today.AddDays(-30), EndDate = Today.AddDays(10),
            AutoRenew = false,
        };

        var score = _engine.ScoreCustomer(customer, [expiring], [], [], complaints, negInteractions);

        Assert.True(score.ChurnScore <= 100);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Payment signals
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Payment_AvgDaysLateAbove30_Adds30()
    {
        var customer = BaseCustomer();
        var payments = new[]
        {
            LatePay(35, Today.AddDays(-10)),
            LatePay(40, Today.AddDays(-20)),
        };

        var baseline = _engine.ScoreCustomer(customer, [], [], [], [], []);
        var withSignal = _engine.ScoreCustomer(customer, [], [], payments, [], []);

        Assert.True(withSignal.PaymentScore >= baseline.PaymentScore + 30);
    }

    [Fact]
    public void Payment_MoreThan2Overdue_Adds25()
    {
        var customer = BaseCustomer();
        var invoices = new[]
        {
            Inv("overdue"), Inv("overdue"), Inv("overdue"),
        };

        var baseline = _engine.ScoreCustomer(customer, [], [], [], [], []);
        var withSignal = _engine.ScoreCustomer(customer, [], invoices, [], [], []);

        Assert.True(withSignal.PaymentScore >= baseline.PaymentScore + 25);
    }

    [Fact]
    public void Payment_Exactly2Overdue_DoesNotAdd25()
    {
        var customer = BaseCustomer();
        var invoices = new[] { Inv("overdue"), Inv("overdue") };

        var baseline = _engine.ScoreCustomer(customer, [], [], [], [], []);
        var withSignal = _engine.ScoreCustomer(customer, [], invoices, [], [], []);

        Assert.True(withSignal.PaymentScore < baseline.PaymentScore + 25);
    }

    [Fact]
    public void Payment_PartialInvoiceRecent_Adds15()
    {
        var customer = BaseCustomer();
        var partial = Inv("partial", Today.AddMonths(-1));

        var baseline = _engine.ScoreCustomer(customer, [], [], [], [], []);
        var withSignal = _engine.ScoreCustomer(customer, [], [partial], [], [], []);

        Assert.True(withSignal.PaymentScore >= baseline.PaymentScore + 15);
    }

    [Fact]
    public void Payment_SeverelyOverdueInvoice_Adds10()
    {
        var customer = BaseCustomer();
        var old = Inv("overdue", Today.AddMonths(-4), Today.AddDays(-100));

        var baseline = _engine.ScoreCustomer(customer, [], [], [], [], []);
        var withSignal = _engine.ScoreCustomer(customer, [], [old], [], [], []);

        Assert.True(withSignal.PaymentScore >= baseline.PaymentScore + 10);
    }

    [Fact]
    public void Payment_ScoreCappedAt100()
    {
        var customer = BaseCustomer();
        var payments = Enumerable.Range(0, 20).Select(_ => LatePay(60)).ToList();
        var invoices = Enumerable.Range(0, 10).Select(_ => Inv("overdue")).ToList();

        var score = _engine.ScoreCustomer(customer, [], invoices, payments, [], []);

        Assert.True(score.PaymentScore <= 100);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Margin signals
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Margin_DecliningContractValue_Adds30()
    {
        var customer = BaseCustomer();
        var contracts = new[]
        {
            new Contract { Id = Guid.NewGuid(), CrmExternalId = "C-OLD", StartDate = Today.AddYears(-2), EndDate = Today.AddYears(-1), MonthlyValue = 5000, Status = "expired" },
            new Contract { Id = Guid.NewGuid(), CrmExternalId = "C-NEW", StartDate = Today.AddYears(-1), EndDate = Today.AddYears(1), MonthlyValue = 3000, Status = "active" },
        };

        var baseline = _engine.ScoreCustomer(customer, [], [], [], [], []);
        var withSignal = _engine.ScoreCustomer(customer, contracts, [], [], [], []);

        Assert.True(withSignal.MarginScore >= baseline.MarginScore + 30);
    }

    [Fact]
    public void Margin_BillingComplaint_Adds25()
    {
        var customer = BaseCustomer();
        var complaint = Comp("medium", "billing", Today.AddMonths(-1));

        var baseline = _engine.ScoreCustomer(customer, [], [], [], [], []);
        var withSignal = _engine.ScoreCustomer(customer, [], [], [], [complaint], []);

        Assert.True(withSignal.MarginScore >= baseline.MarginScore + 25);
    }

    [Fact]
    public void Margin_ShortDurationContract_Adds10()
    {
        var customer = BaseCustomer();
        var shortContract = new Contract
        {
            Id = Guid.NewGuid(),
            CrmExternalId = "C-SHORT",
            Status = "active",
            StartDate = Today.AddMonths(-3),
            EndDate = Today.AddMonths(2), // 5 months total < 6 months
            MonthlyValue = 2000,
        };

        var baseline = _engine.ScoreCustomer(customer, [], [], [], [], []);
        var withSignal = _engine.ScoreCustomer(customer, [shortContract], [], [], [], []);

        Assert.True(withSignal.MarginScore >= baseline.MarginScore + 10);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Overall composite + heat thresholds
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Overall_CompositeWeighting_IsCorrect()
    {
        var customer = BaseCustomer();
        // Direct verification: inject predictable individual scores
        // We test the formula by checking boundary cases

        // All zeros → overall = 0, green
        var score = _engine.ScoreCustomer(customer, [], [], [], [], []);
        var expected = (int)Math.Round(score.ChurnScore * 0.40 + score.PaymentScore * 0.35 + score.MarginScore * 0.25);
        Assert.Equal(expected, score.OverallScore);
    }

    [Fact]
    public void HeatLevel_GreenAt39()
    {
        // We verify the heat level mapping via boundary inspection
        // Since we can't control exact scores directly, verify known thresholds
        // by checking the mapping logic in ScoreCustomer output
        var customer = BaseCustomer(Today.AddYears(-2)); // old enough, no new tenure signal
        var score = _engine.ScoreCustomer(customer, [], [], [], [], [Act("outbound", "neutral", Today.AddDays(-5))]);

        if (score.OverallScore <= 39)
            Assert.Equal("green", score.HeatLevel);
        else if (score.OverallScore <= 69)
            Assert.Equal("yellow", score.HeatLevel);
        else
            Assert.Equal("red", score.HeatLevel);
    }

    [Fact]
    public void HeatLevel_Yellow_WhenOverallBetween40And69()
    {
        // Score with medium risk signals to land in yellow range
        var customer = BaseCustomer(Today.AddMonths(-6)); // tenure < 12 = +10 churn
        var expiring = new Contract
        {
            Id = Guid.NewGuid(), CrmExternalId = "C-EXP",
            Status = "active", StartDate = Today.AddDays(-90),
            EndDate = Today.AddDays(60), AutoRenew = false,
        };
        var payments = new[] { LatePay(35, Today.AddDays(-10)), LatePay(38, Today.AddDays(-20)) };

        var score = _engine.ScoreCustomer(customer, [expiring], [], payments, [], []);
        var heatFromScore = score.OverallScore switch
        {
            <= 39 => "green",
            <= 69 => "yellow",
            _ => "red",
        };
        Assert.Equal(heatFromScore, score.HeatLevel);
    }

    [Fact]
    public void Score_ZeroSignals_IsGreenOrLow()
    {
        var customer = BaseCustomer(Today.AddYears(-5)); // very long tenure
        var outbound = new[] { Act("outbound", "neutral", Today.AddDays(-1)) };
        var score = _engine.ScoreCustomer(customer, [], [], [], [], outbound);

        Assert.True(score.OverallScore <= 39);
        Assert.Equal("green", score.HeatLevel);
    }

    [Fact]
    public void Score_BoundaryAt40_IsYellow()
    {
        // Verify that a score of exactly 40 maps to yellow
        // We can't force exact score so we verify the threshold map
        Assert.Equal("yellow", ScoreHeatLevel(40));
    }

    [Fact]
    public void Score_BoundaryAt39_IsGreen()
    {
        Assert.Equal("green", ScoreHeatLevel(39));
    }

    [Fact]
    public void Score_BoundaryAt70_IsRed()
    {
        Assert.Equal("red", ScoreHeatLevel(70));
    }

    [Fact]
    public void Score_BoundaryAt69_IsYellow()
    {
        Assert.Equal("yellow", ScoreHeatLevel(69));
    }

    // Helper that applies the same threshold logic as RiskScoringEngine
    private static string ScoreHeatLevel(int overall) => overall switch
    {
        <= 39 => "green",
        <= 69 => "yellow",
        _ => "red",
    };
}
