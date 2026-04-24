# Prompt 06 — CRM CSV Import Service

**Agent**: `general-purpose`  
**Phase**: 1 — Foundation  
**Status**: DONE  
**Depends on**: Prompt 02 (backend scaffold)

---

Implement `CrmImportService` in the Customer Portfolio Thermometer backend to read the real energy-sector CRM CSV files from `crm-data/` and upsert them into PostgreSQL.

Read `documentation/plan.md` (CRM CSV Data Source section) and `documentation/plan/data-flow.md` (Stage 1) for the full file list and import sequence.

TypeScript column definitions for every file are in `frontend/src/app/core/models/crm-schema.model.ts` — use these as the authoritative column reference when writing C# field mappings.

---

## Files to create / modify

```
backend/src/PortfolioThermometer.Infrastructure/Services/CrmImportService.cs   (replace stub)
backend/src/PortfolioThermometer.Infrastructure/Csv/CsvReader.cs               (new)
backend/src/PortfolioThermometer.Infrastructure/Csv/Mappers/                   (new folder)
    OrganizationCsvMapper.cs
    ContractCsvMapper.cs
    ConnectionCsvMapper.cs
    MeterReadCsvMapper.cs
    InteractionCsvMapper.cs
    InvoiceCsvMapper.cs
backend/src/PortfolioThermometer.Api/appsettings.json                          (add CrmDataPath)
```

---

## Configuration

Add to `appsettings.json`:

```json
{
  "CrmDataPath": "/data/crm-data"
}
```

In `docker-compose.yml`, mount the folder read-only into the backend container:

```yaml
volumes:
  - ./crm-data:/data/crm-data:ro
```

---

## CsvReader utility

Create `CsvReader.cs` in `Infrastructure/Csv/`:

- Strip UTF-8 BOM from the first line before parsing
- Comma delimiter; handle quoted fields (`"value,with,commas"`)
- Skip blank lines
- Return `IEnumerable<Dictionary<string, string>>` (header-keyed rows)
- Expose `ReadAllAsync(string filePath)` and `ReadHeaderAsync(string filePath)`
- For multi-file sets (Meter Read 1–8), expose `ReadMultipleAsync(IEnumerable<string> paths)`

---

## Import sequence and field mappings

### Step 1 — Customers

Source: `ERPSQLServer/[Confidential] Organizations.csv`  
Filter: rows where `OrganizationTypeId == "2"` (Customer)

| CSV column | Domain field | Notes |
|---|---|---|
| `OrganizationId` | `crm_external_id` | string |
| `Name` | `name` + `company_name` | pseudonymised hash |
| `OrganizationTypeId` | `segment` | map: 2=customer, 5=collective, 6=company |
| `TransStartDate` | `onboarding_date` | parse as date |
| — | `is_active` | default `true` |

Supplement with `Contract-Customer-Connection-BrokerDebtor.csv` to associate `CustomerNumber` and `DebtorNumber` with the customer record (store in `crm_external_id` as the CustomerNumber when available).

### Step 2 — Contracts

Source: `ERPSQLServer/[Confidential] Contracts.csv`

| CSV column | Domain field | Notes |
|---|---|---|
| `ContractId` | `crm_external_id` | |
| `ContractType` | `contract_type` | "Customer" or "Period" |
| `ContractReference` | — | store as metadata |
| `ProductId` | — | 1=Electricity, 2=Gas; store as label on contract |
| `StartDate` | `start_date` | |
| `EndDate` | `end_date` | `9999-12-31` → `null` |
| `CurrentAgreedAmount` | `monthly_value` | decimal |
| — | `currency` | default "EUR" |
| — | `status` | derive: EndDate < today → "expired"; else "active" |
| — | `auto_renew` | default `false` (no source column) |

Link contracts to customers via `Contract-Customer-Connection-BrokerDebtor.csv` (join on `ContractID` → `CustomerNumber` → `crm_external_id`).

Also import `Contract Price.csv` and `Price Proposition.csv` (same schema: `ContractUniqueIdentifier`, `StartDate`, `EndDate`, `Price`, `Description`) — store as contract metadata or a flat `contract_prices` table for use by the margin risk scorer.

### Step 3 — Connections (energy-specific)

Source: `ERPSQLServer/[Confidential] Connections.csv`

Add a `connections` table to the schema (migration required):

| Column | Type | Source |
|---|---|---|
| `id` | UUID PK | |
| `crm_external_id` | VARCHAR | `ConnectionId` |
| `customer_id` | UUID FK | via Contract-Customer-Connection-BrokerDebtor join |
| `ean` | VARCHAR(50) | `EAN` (pseudonymised) |
| `product_type` | VARCHAR(20) | `ProductType` (Electricity/Gas) |
| `delivery_type` | VARCHAR(10) | `DeliveryType` (LDN/ODN/NA) |
| `connection_type_id` | INTEGER | `ConnectionTypeId` |
| `imported_at` | TIMESTAMPTZ | |

### Step 4 — Meter reads

Sources (scan all):
- `ERPSQLServer/[Confidential] ConnectionMeterReads.csv` (aggregated per connection)
- `ArchievingSolution/Generic/[Confidential] Meter Read_1.csv` through `Meter Read_8.csv`

Add a `meter_reads` table (migration required):

| Column | Type | Source |
|---|---|---|
| `id` | UUID PK | |
| `connection_id` | UUID FK | via EAN → connections.ean |
| `crm_external_id` | VARCHAR | `UsageID` / `EANUniqueIdentifier+StartDate` |
| `start_date` | DATE | `StartDate` |
| `end_date` | DATE | `EndDate` |
| `consumption` | DECIMAL(15,4) | `Consumption` / `Amount` |
| `unit` | VARCHAR(10) | `Unit` (kWh / m3) |
| `usage_type` | VARCHAR(20) | `UsageType` (UsageHigh/UsageLow) |
| `direction` | VARCHAR(20) | `Direction` (Consumption/Production) |
| `quality` | VARCHAR(20) | `Quality` |
| `imported_at` | TIMESTAMPTZ | |

### Step 5 — Interactions

Sources:
- `ERPSQLServer/[Confidential] OrganizationContacts.csv`
- `ERPSQLServer/[Confidential] ConnectionContacts.csv`
- `ERPSQLServer/[Confidential] LastConnectionContacts.csv`

| CSV column | Domain field | Notes |
|---|---|---|
| `ContactDate` | `interaction_date` | |
| `Subject` | `channel` + churn signal | "Cancellation" is a high churn signal |
| `Report` | `summary` | pseudonymised hash — preserve as-is |
| `ContactPerson` | — | pseudonymised |
| `UserName` | — | pseudonymised |
| — | `direction` | default "inbound" |
| — | `sentiment` | null (no source column) |

Link to customer via `OrganizationId` or via connection → contract → customer join chain.

### Step 6 — Invoice archive index

Sources (must scan both):
- `ArchievingSolution/[Confidential] Look-up Customer Data_1.csv`
- `ArchievingSolution/[Confidential] Look-up Customer Data_2.csv`

| CSV column | Domain field | Notes |
|---|---|---|
| `Invoice number` | `invoice_number` + `crm_external_id` | |
| `Customer number` | → customer FK | match to customer by CustomerNumber |
| `Debtor number` | — | store as metadata |
| `Collective name` | — | store as metadata |
| — | `status` | default "unknown" (no status column in archive) |
| — | `issued_date` / `due_date` | null (not in source) |
| — | `amount` | null (not in source) |

---

## Implementation notes

1. Use `CancellationToken` throughout all async methods.
2. Log progress at each step: `"Importing customers from Organizations.csv…"`, `"Upserted {n} customers."`.
3. Return `ImportResult` with counts per entity type and any per-row errors (do not abort the whole import on a single bad row — log and continue).
4. BOM stripping: `StreamReader` with `detectEncodingFromByteOrderMarks: true` handles this automatically in .NET.
5. Open-ended dates: if `EndDate` string is `"9999-12-31"` or `"99991231"` → set domain field to `null`.
6. The EAN field in `Connections.csv` may be blank for some rows — skip connections with no EAN.
7. Meter Read files 1–8 are large — read with `StreamReader` line-by-line rather than loading into memory.

---

## Tests to write first (TDD)

```
CrmImportServiceTests:
- ImportCustomers_ParsesOrganizationsCsv_UpsertsCorrectCount
- ImportCustomers_SkipsNonCustomerOrganizationTypes
- ImportContracts_MapsOpenEndedDate_SetsEndDateNull
- ImportContracts_DeriveStatus_ExpiredWhenEndDatePast
- ImportInteractions_CancellationSubject_FlagsAsChurnSignal
- ImportInvoices_ScansBootFiles_CombinesIntoSingleList
- CsvReader_StripsBom_ParsesHeaderCorrectly
- CsvReader_HandlesQuotedFields_WithCommasInValue
```

Reference: `documentation/plan.md`, `documentation/plan/data-flow.md` (Stage 1), `frontend/src/app/core/models/crm-schema.model.ts`
