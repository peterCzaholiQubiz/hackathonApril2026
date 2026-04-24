# Prompt 13 — Meter Read Lite: Strip Heavy Import + Hourly Generation API

**Agent**: `general-purpose`
**Phase**: 3 — UX Enhancement
**Status**: TODO
**Depends on**: Prompt 12 (Missing Tables & Import Steps), Prompt 08 (Angular Dashboard), Prompt 09 (Angular Customer Views)

---

## Context

The CSV meter read files (`ConnectionMeterReads.csv` and `Meter Read_1-8.csv`) are very large and slow the import down significantly. For the hackathon demo:

1. **Slim the import**: cap customers at 1 000 and remove the meter read step — already applied directly to `CrmImportService.cs` and `ImportResult.cs`.
2. **Add** a backend generation endpoint that accepts a consumption profile + time period and generates **one `MeterRead` row per hour** for the full period, persisting all rows to `meter_reads`.
3. **Add** a frontend component with profile/period selectors that calls the API and visualises the daily-aggregated result as a chart.

### Already-applied import changes (do not re-implement)

The following changes are already in the codebase — this prompt only adds the generation API on top:

- `CrmImportService.ImportAllAsync` no longer calls `ImportMeterReadsAsync`. The method body is preserved but unwired. Neither `ConnectionMeterReads.csv` nor `Meter Read_1-8.csv` are read.
- `ImportCustomersAsync` applies `.Take(1000)` after filtering `OrganizationTypeId == "2"`. All downstream steps (contracts, connections, interactions, invoices) are automatically scoped to those 1 000 customers because they filter via the `debtorRefToCustomerId` dictionary.
- `ImportResult` no longer has a `MeterReadsImported` field.

---

## Part 1 — Backend: Update `MeterRead` Model for Hourly Precision

### 2.1 — Change `StartDate` / `EndDate` to `DateTimeOffset`

File: `backend/src/PortfolioThermometer.Core/Models/MeterRead.cs`

Replace the two `DateOnly?` properties with `DateTimeOffset?`:

```csharp
public DateTimeOffset? StartDate { get; set; }
public DateTimeOffset? EndDate { get; set; }
```

### 2.2 — Update EF configuration

File: `backend/src/PortfolioThermometer.Infrastructure/Data/Configurations/MeterReadConfiguration.cs`

Remove any `.HasConversion<DateOnlyConverter>()` on those two columns and configure them as `timestamptz`:

```csharp
builder.Property(m => m.StartDate).HasColumnName("start_date").HasColumnType("timestamptz");
builder.Property(m => m.EndDate).HasColumnName("end_date").HasColumnType("timestamptz");
```

### 2.3 — Update `init.sql`

File: `database/init.sql`

Change the `meter_reads` table column types:

```sql
start_date  TIMESTAMPTZ,
end_date    TIMESTAMPTZ,
```

### 2.4 — EF migration

Generate a migration after updating the model:

```
dotnet ef migrations add MeterReadsDateTimeOffset \
  --project backend/src/PortfolioThermometer.Infrastructure \
  --startup-project backend/src/PortfolioThermometer.Api
```

---

## Part 3 — Backend: Hourly Meter Read Generation Service

### 3.1 — Request / Response DTOs

Create in `backend/src/PortfolioThermometer.Api/Models/MeterReadGeneration.cs`:

```csharp
namespace PortfolioThermometer.Api.Models;

public enum ConsumptionProfile
{
    LowConsumer,
    HighConsumer,
    LowDaytime,
    HighDaytime,
    SolarProducer,
    Industrial,
}

public enum GenerationPeriod { ThreeMonths, SixMonths, OneYear, TwoYears }

public record GenerateMeterReadsRequest(
    Guid CustomerId,
    ConsumptionProfile Profile,
    GenerationPeriod Period
);

// One entry per calendar day, aggregated from hourly rows for the chart
public record DailyMeterReadSummary(
    string Date,               // "2025-01-15"
    decimal ConsumptionHigh,   // sum of UsageHigh rows for that day
    decimal ConsumptionLow,    // sum of UsageLow rows for that day
    decimal TotalConsumption,
    decimal Production         // 0 for non-solar
);

public record GenerateMeterReadsResponse(
    Guid CustomerId,
    ConsumptionProfile Profile,
    GenerationPeriod Period,
    int TotalHourlyRowsGenerated,
    IReadOnlyList<DailyMeterReadSummary> DailySummary  // for chart rendering
);
```

The API persists hourly rows to the DB and returns **daily aggregates** in the response — this keeps the payload small while the full-resolution data lives in the database.

### 3.2 — Service interface

File: `backend/src/PortfolioThermometer.Core/Interfaces/IMeterReadGenerationService.cs`

```csharp
using PortfolioThermometer.Api.Models;

namespace PortfolioThermometer.Core.Interfaces;

public interface IMeterReadGenerationService
{
    Task<GenerateMeterReadsResponse> GenerateAsync(
        GenerateMeterReadsRequest request,
        CancellationToken ct = default);
}
```

### 3.3 — Profile parameter table

Define a private static dictionary inside the implementation mapping `ConsumptionProfile` to:

| Profile | AnnualKwh | PeakHourFactor | OffPeakHourFactor | HasProduction |
|---|---|---|---|---|
| LowConsumer | 2 500 | 1.2 | 0.8 | false |
| HighConsumer | 12 000 | 1.2 | 0.8 | false |
| LowDaytime | 6 000 | 0.4 | 2.2 | false |
| HighDaytime | 8 000 | 1.8 | 0.2 | false |
| SolarProducer | 4 500 | 1.2 | 0.8 | true |
| Industrial | 80 000 | 1.05 | 0.95 | false |

**Peak hours**: 07:00–21:59 (15 hours/day).  
**Off-peak hours**: 22:00–06:59 (9 hours/day).

The factors are applied so that the weighted average over 24 hours equals 1.0:
`(15 × peakFactor + 9 × offPeakFactor) / 24 ≈ 1.0`.

### 3.4 — Generation service implementation

File: `backend/src/PortfolioThermometer.Infrastructure/Services/MeterReadGenerationService.cs`

```csharp
namespace PortfolioThermometer.Infrastructure.Services;

public sealed class MeterReadGenerationService(AppDbContext db) : IMeterReadGenerationService
{
    public async Task<GenerateMeterReadsResponse> GenerateAsync(
        GenerateMeterReadsRequest request,
        CancellationToken ct = default)
    { ... }
}
```

#### Algorithm — per hourly slot

For each hour `h` in `[startUtc, endUtc)` (exclusive end):

1. **`startSlot`** = `h` (UTC), **`endSlot`** = `h + 1 hour`.
2. **Seasonal factor** based on `h.Month`:

   | Month | Factor |
   |---|---|
   | 1 (Jan), 2 (Feb), 12 (Dec) | 1.35 |
   | 3 (Mar), 11 (Nov) | 1.15 |
   | 4 (Apr), 10 (Oct) | 0.95 |
   | 5 (May), 9 (Sep) | 0.80 |
   | 6 (Jun), 7 (Jul), 8 (Aug) | 0.65 |

3. **Base hourly kWh** = `annualKwh / 8760`.
4. **Time-of-day factor**: if `h.Hour >= 7 && h.Hour < 22` → `peakHourFactor`, else → `offPeakHourFactor`.
5. **UsageType**: if peak hour → `"UsageHigh"`, else → `"UsageLow"`.
6. **Deterministic jitter** ±6 %:
   ```
   jitter = 1.0 + Math.Sin((h.DayOfYear * 24 + h.Hour + profileIndex) * 1.9) * 0.06
   ```
   `profileIndex` = zero-based index of the profile in the table above.
7. **Consumption** = `baseHourly × seasonalFactor × timeOfDayFactor × jitter`, rounded to 4 decimal places.
8. **Direction** = `"Consumption"` (always for non-solar; see below for solar).
9. **Production** (solar only):
   - Production only occurs during daylight hours: `h.Hour >= 6 && h.Hour < 20`.
   - Solar seasonal production factor:

     | Month | SolarFactor |
     |---|---|
     | 6 (Jun) | 0.55 |
     | 7 (Jul) | 0.60 |
     | 8 (Aug) | 0.55 |
     | 5 (May), 9 (Sep) | 0.35 |
     | 4 (Apr), 10 (Oct) | 0.20 |
     | All other months | 0.05 |

   - `productionHourly = baseHourly × solarFactor × jitter` (same jitter, rounded to 4 dp).
   - Store production as a **separate** `MeterRead` row with the same slot, `Direction = "Production"`, `UsageType = "UsageHigh"`, and `Consumption = productionHourly`.

#### Period → date range

| Period | Month count |
|---|---|
| ThreeMonths | 3 |
| SixMonths | 6 |
| OneYear | 12 |
| TwoYears | 24 |

`startUtc` = midnight UTC on the first day of the calendar month that is `monthCount` months before the current month.  
`endUtc` = midnight UTC on the first day of the **current** calendar month.

#### Persistence

- **`CrmExternalId`** format: `"GEN-{customerId:N}-{slotStart:yyyyMMddHH}-{usageType[0]}"` (e.g. `"GEN-abc...def-2025010108-H"` for UsageHigh, `-L` for UsageLow, `-P` for Production).
- **Upsert logic**: delete all existing `meter_reads` rows where `CrmExternalId` starts with `"GEN-{customerId:N}-"` before inserting. This avoids conflict on re-generation without a complex merge.
- Batch inserts using `AddRangeAsync` and a single `SaveChangesAsync` at the end.
- `ConnectionId = null`, `Source = "Generated"`, `Quality = "Estimated"`, `Unit = "kWh"`.

#### Build the daily summary for the response

After generating the in-memory list, group rows by `startSlot.Date`:
- Sum `ConsumptionHigh` (UsageHigh, Direction = Consumption rows).
- Sum `ConsumptionLow` (UsageLow, Direction = Consumption rows).
- Sum `Production` (Direction = Production rows).
- `TotalConsumption = ConsumptionHigh + ConsumptionLow`.

Return the daily summary sorted ascending by date.

### 3.5 — Controller

File: `backend/src/PortfolioThermometer.Api/Controllers/MeterReadController.cs`

```csharp
[ApiController]
[Route("api/meter-reads")]
public sealed class MeterReadController(IMeterReadGenerationService generator) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<ActionResult<GenerateMeterReadsResponse>> Generate(
        [FromBody] GenerateMeterReadsRequest request,
        CancellationToken ct)
    {
        var result = await generator.GenerateAsync(request, ct);
        return Ok(result);
    }
}
```

### 3.6 — Register service

File: `backend/src/PortfolioThermometer.Api/Program.cs`

```csharp
builder.Services.AddScoped<IMeterReadGenerationService, MeterReadGenerationService>();
```

---

## Part 4 — Frontend: Meter Read Simulator

### 4.1 — Models

File: `frontend/src/app/core/models/meter-read.model.ts`

```typescript
export type ConsumptionProfile =
  | 'LowConsumer'
  | 'HighConsumer'
  | 'LowDaytime'
  | 'HighDaytime'
  | 'SolarProducer'
  | 'Industrial';

export type GenerationPeriod = 'ThreeMonths' | 'SixMonths' | 'OneYear' | 'TwoYears';

export interface GenerateMeterReadsRequest {
  customerId: string;
  profile: ConsumptionProfile;
  period: GenerationPeriod;
}

export interface DailyMeterReadSummary {
  date: string;
  consumptionHigh: number;
  consumptionLow: number;
  totalConsumption: number;
  production: number;
}

export interface GenerateMeterReadsResponse {
  customerId: string;
  profile: ConsumptionProfile;
  period: GenerationPeriod;
  totalHourlyRowsGenerated: number;
  dailySummary: DailyMeterReadSummary[];
}

export interface ConsumptionProfileOption {
  value: ConsumptionProfile;
  label: string;
  description: string;
}

export interface PeriodOption {
  value: GenerationPeriod;
  label: string;
}
```

### 4.2 — HTTP service

File: `frontend/src/app/core/services/meter-read.service.ts`

```typescript
@Injectable({ providedIn: 'root' })
export class MeterReadService {
  private readonly http = inject(HttpClient);

  readonly profileOptions: ConsumptionProfileOption[] = [
    { value: 'LowConsumer',    label: 'Low Consumer',    description: 'Small household or low-usage SMB, ~2 500 kWh/year' },
    { value: 'HighConsumer',   label: 'High Consumer',   description: 'Large household or medium business, ~12 000 kWh/year' },
    { value: 'LowDaytime',     label: 'Low Daytime',     description: 'Night-shift or off-peak heavy user' },
    { value: 'HighDaytime',    label: 'High Daytime',    description: 'Office or retail — heavy peak / daytime usage' },
    { value: 'SolarProducer',  label: 'Solar Producer',  description: 'Prosumer with rooftop panels' },
    { value: 'Industrial',     label: 'Industrial',      description: 'High-voltage industrial connection, ~80 000 kWh/year' },
  ];

  readonly periodOptions: PeriodOption[] = [
    { value: 'ThreeMonths', label: '3 Months' },
    { value: 'SixMonths',   label: '6 Months' },
    { value: 'OneYear',     label: '1 Year'   },
    { value: 'TwoYears',    label: '2 Years'  },
  ];

  generate(request: GenerateMeterReadsRequest): Observable<GenerateMeterReadsResponse> {
    return this.http.post<GenerateMeterReadsResponse>('/api/meter-reads/generate', request);
  }
}
```

### 4.3 — Feature component

File: `frontend/src/app/features/meter-reads/meter-reads.component.ts`

Standalone Angular component.

#### Template layout

```
┌──────────────────────────────────────────────────────────────┐
│  Meter Read Simulator                                        │
│  ──────────────────────────────────────────────────────────  │
│  Customer: [select ▾]  Profile: [Low Consumer ▾]            │
│  Period:   [1 Year ▾]  [Generate]                           │
│                                                              │
│  ┌─ Stacked Bar Chart (daily aggregated) ────────────────┐  │
│  │  ■ Peak (UsageHigh)  ■ Off-Peak (UsageLow)            │  │
│  │  ■ Production (solar only)                            │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                              │
│  Total kWh  │  Daily Avg  │  Peak %  │  Net (solar only)    │
│  ─────────────────────────────────────────────────────────  │
│  Hourly rows stored: {totalHourlyRowsGenerated}             │
└──────────────────────────────────────────────────────────────┘
```

#### Component requirements

- **Customer selector**: populated by calling the existing `CustomerService.getCustomers()`.
- **Profile selector**: 6 options from `MeterReadService.profileOptions`.
- **Period selector**: 4 options from `MeterReadService.periodOptions`.
- **Generate button**: calls `MeterReadService.generate(request)`. Disabled while in flight.
- **Loading state**: show `LoadingSkeletonComponent` while awaiting the response.
- **Chart**: stacked bar chart using `dailySummary` from the response.
  - X-axis: day labels from `DailyMeterReadSummary.date` (show abbreviated month+day).
  - Y-axis: kWh.
  - Series "Peak (UsageHigh)" — blue.
  - Series "Off-Peak (UsageLow)" — teal.
  - Series "Production" — amber. Only included when `profile === 'SolarProducer'`.
- **Summary cards** (computed from `dailySummary`):
  - **Total kWh** — sum of `totalConsumption`.
  - **Daily Avg** — average `totalConsumption`.
  - **Peak Share** — `sum(consumptionHigh) / sum(totalConsumption)` as percentage.
  - **Net Consumption** (solar only) — `sum(totalConsumption) − sum(production)`.
- **Hourly row count**: display `totalHourlyRowsGenerated` as a small info label beneath the summary.
- **Error state**: inline error message on API failure.

### 4.4 — Chart library

Check `frontend/package.json`. Re-use whatever charting library is already present. If none, add:

```
npm install apexcharts ng-apexcharts
```

### 4.5 — Route

File: `frontend/src/app/app.routes.ts`

```typescript
{
  path: 'meter-reads',
  loadComponent: () =>
    import('./features/meter-reads/meter-reads.component').then(
      m => m.MeterReadsComponent
    ),
}
```

### 4.6 — Navigation link

Add a "Meter Reads" nav link in the existing sidebar/nav component.

---

## Part 5 — Tests (TDD — write before implementing)

### Backend: generation service

File: `backend/tests/PortfolioThermometer.Infrastructure.Tests/Services/MeterReadGenerationServiceTests.cs`

```
GenerateAsync_OneYear_Generates8760ConsumptionRows
GenerateAsync_SolarProducer_OneYear_GeneratesAdditionalProductionRows
GenerateAsync_PeakHours_UsageTypeIsHigh
GenerateAsync_OffPeakHours_UsageTypeIsLow
GenerateAsync_HighDaytime_PeakConsumptionExceedsOffPeakConsumption
GenerateAsync_LowDaytime_OffPeakConsumptionExceedspeakConsumption
GenerateAsync_WinterMonth_ConsumptionHigherThanSummerMonth
GenerateAsync_SolarProducer_SummerDaytimeProductionHigherThanWinterDaytime
GenerateAsync_NonSolarProfile_ProductionRowsAreAbsent
GenerateAsync_JitterProducesDifferentValuesForAdjacentHours
GenerateAsync_DailySummaryCountMatchesDaysInPeriod
GenerateAsync_DailySummaryTotalsMatchHourlyRollup
GenerateAsync_Idempotent_RerunDeletesAndReinserts
GenerateAsync_ThreeMonths_Generates_ApproxCorrectHourCount
GenerateAsync_TwoYears_Generates_ApproxCorrectHourCount
```

### Backend: controller

```
Generate_Returns200_WithValidRequest
Generate_Returns400_WhenCustomerIdIsEmpty
```

### Frontend: service

```
generate_postsToCorrectEndpoint
generate_mapsResponseCorrectly
profileOptions_ContainsAll6Profiles
periodOptions_ContainsAll4Periods
```

### Frontend: component

```
renders_all_6_profile_options_in_selector
renders_all_4_period_options_in_selector
generate_button_disabled_while_loading
chart_renders_after_successful_response
solar_profile_includes_production_series
non_solar_profile_omits_production_series
summary_total_matches_sum_of_daily_summaries
hourly_row_count_label_displays_totalHourlyRowsGenerated
error_message_shown_on_api_failure
```

---

## Implementation notes

- **1-year period = 8 760 hourly consumption rows** (+ up to ~5 000 production rows for solar). For a 2-year period that is ~17 520 consumption rows. Batch all inserts with `AddRangeAsync`; call `SaveChangesAsync` once.
- **Re-generation**: delete all rows with `CrmExternalId LIKE 'GEN-{customerId:N}-%'` in a single `ExecuteDeleteAsync` call before re-inserting. This is simpler and faster than per-row upsert at this volume.
- **Response payload**: the API returns `DailySummary` (90 / 180 / 365 / 730 entries) — not the raw hourly rows — so the JSON stays small regardless of period.
- **Datetime precision**: `DateTimeOffset` in C#, `timestamptz` in Postgres.
- **No changes to risk scoring** — the generation endpoint is fully independent.
- Keep `ImportMeterReadsAsync` in `CrmImportService` (un-wired). Do not delete it.

---

## Acceptance criteria

- [ ] `dotnet build` and `ng build` pass after all changes.
- [ ] `POST /api/meter-reads/generate` returns 200 with `DailySummary` and `TotalHourlyRowsGenerated`.
- [ ] A 1-year generation stores exactly 8 760 consumption rows (± daylight-saving edge case) plus production rows for solar.
- [ ] Re-generating with the same `customerId` + `profile` replaces the previous rows cleanly.
- [ ] Peak-hour rows have `usage_type = 'UsageHigh'`; off-peak rows have `usage_type = 'UsageLow'`.
- [ ] Solar producer rows include `Direction = 'Production'` entries during daylight hours.
- [ ] Frontend chart renders `DailySummary` as a stacked bar chart.
- [ ] Solar producer shows production series; all other profiles do not.
- [ ] All unit tests pass.
- [ ] Import pipeline no longer reads meter read CSV files.
