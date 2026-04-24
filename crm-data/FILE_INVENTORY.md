# File Inventory

This document lists all files contained within the three subfolders of the root directory: `ArchievingSolution`, `DevOps`, and `ERPSQLServer`.

> **Note on pseudonymisation.** The data files in this archive contain **pseudonymised** values for every field that could identify a person, organisation, address or physical asset. EAN codes, customer and debtor names, meter numbers, free-text contact reports, contract references and similar identifiers have been replaced with stable hash-like tokens before export — the same real value always maps to the same token, so joins across files still work, but the original value cannot be recovered from the token. Numeric values (consumption, prices, tariffs, amounts), dates, status codes and categorical fields (product type, connection type, usage type, direction, etc.) are preserved as-is and reflect real energy-market data.

---

## 1. ArchievingSolution

### Root

- **[Confidential] Look-up Customer Data_1.csv**
  Look-up index that maps each archived invoice back to the folder where its supporting documents live on the archive drive. Each row contains a `Collective name` (a hashed group key used to keep related parties together), a `Customer number` (the end customer), a `Debtor number` (the legal/billing party that the invoice is addressed to — one customer can have multiple debtors over time), the `Invoice number`, and the `File location`, which is the full path on the archive drive. This is the first half of the split dataset and is the primary entry point when someone needs to locate the physical documents for a specific invoice, debtor or customer.

- **[Confidential] Look-up Customer Data_2.csv**
  Second half of the same look-up index, with identical columns to `Look-up Customer Data_1.csv`. The content was split into two files purely because of row volume — together the two files form one continuous index, and a search by customer, debtor or invoice number must scan both.

### Generic

- **[Confidential] Contract Price.csv**
  The individual price lines that were actually agreed with a customer in a specific contract — the tariffs the customer really pays. Columns: `ContractUniqueIdentifier` (the contract the line belongs to), `StartDate`, `EndDate` (the period during which the price is valid — `99991231` is used as an "open-ended" end date), `Price` (the monetary value, the unit depending on the line — €/kWh, €/m³, €/year, etc.), and a Dutch `Description` explaining what is being charged. Typical descriptions include `"Vaste Prijs LDN Gas G1"` (fixed delivery price for gas in zone G1), `"Vaste Prijs Vastrecht Gas"` (fixed standing charge for gas) and `"Opslag Hoog Resultaat"` (peak-rate surcharge for electricity). Together these lines reconstruct the commercial terms that were used to produce an invoice.

- **[Confidential] Meter Read_1.csv**
  Meter-reading history per EAN. An EAN (European Article Number) is the 18-digit code that uniquely identifies an electricity or gas connection point in the Dutch/Belgian grid — every house, building or meter-controlled asset has one, and it is the main key used throughout the Dutch energy market. Each row records the consumption (or production) measured on that EAN over a period. Columns: `EANUniqueIdentifier`, `StartDate`, `EndDate` (the period covered by the reading), `Consumption` (delta over the period), `Position` / `PreviousPosition` (the raw meter counter values at the end and start of the period), `Source` (where the reading came from, e.g. `DVEP`, `Customer`, grid operator), `Quality` (`Estimated`, `Customer`, `Actual`, etc.), `UsageType` (`UsageLow` for off-peak / night, `UsageHigh` for peak / day — dual-tariff electricity meters produce two rows per period), `Direction` (`Consumption` for energy drawn from the grid, `Production` for energy fed back in by solar panels), `MeterIdentifier` (physical meter serial) and `MeterFactor` (multiplier used when the meter reads a scaled value, e.g. via a CT). The full meter-read history is split across eight files because of its size; a query for a given EAN may need to scan all of them.

- **[Confidential] Meter Read_2.csv**
  Continuation of the meter-reading dataset. Same columns and semantics as `Meter Read_1.csv`.

- **[Confidential] Meter Read_3.csv**
  Continuation of the meter-reading dataset. Same columns and semantics as `Meter Read_1.csv`.

- **[Confidential] Meter Read_4.csv**
  Continuation of the meter-reading dataset. Same columns and semantics as `Meter Read_1.csv`.

- **[Confidential] Meter Read_5.csv**
  Continuation of the meter-reading dataset. Same columns and semantics as `Meter Read_1.csv`.

- **[Confidential] Meter Read_6.csv**
  Continuation of the meter-reading dataset. Same columns and semantics as `Meter Read_1.csv`.

- **[Confidential] Meter Read_7.csv**
  Continuation of the meter-reading dataset. Same columns and semantics as `Meter Read_1.csv`.

- **[Confidential] Meter Read_8.csv**
  Final part of the meter-reading dataset. Same columns and semantics as `Meter Read_1.csv`.

- **[Confidential] Price Proposition.csv**
  Proposition prices — the "catalogue" tariffs that belong to the commercial product (the proposition) a contract was sold under, as opposed to the contract-specific prices in `Contract Price.csv`. Together these two files show both the standard offer and the actually agreed price, which is what auditors typically need to reconcile an invoice. Columns are identical to `Contract Price.csv`: `ContractUniqueIdentifier`, `StartDate`, `EndDate`, `Price`, `Description` (Dutch labels such as `"Vaste Prijs Vastrecht Gas"` — fixed standing charge for gas, `"Vaste Prijs Laag LDN Elektriciteit"` — fixed off-peak electricity delivery price).

- **[Confidential] Timeslices - CaptarCode.csv**
  Time-sliced history of the Captar (tariff classification) code attached to each EAN. The captar code determines which grid-operator tariff category applies to the connection; it can change when e.g. a customer becomes exempt. Columns: `EANUniqueIdentifier`, `StartDate`, `EndDate`, `Description` (Dutch, e.g. `"Nultarief electriciteit"` — zero tariff for electricity).

- **[Confidential] Timeslices - ConnectionType.csv**
  Time-sliced history of the connection type of each EAN. "Connection type" describes the role of the connection in the grid — typically `"Levering"` (delivery: energy flowing from the grid to the customer) versus `"Teruglevering"` (return delivery: energy injected back into the grid, e.g. by a solar installation). Columns: `EANUniqueIdentifier`, `StartDate`, `EndDate`, `Description`.

- **[Confidential] Timeslices - EnergyDeliveryStatus.csv**
  Time-sliced history of whether energy was being delivered on an EAN. Columns: `EANUniqueIdentifier`, `StartDate`, `EndDate`, `Description`. Typical Dutch values: `"Afgesloten"` (disconnected — no energy being delivered), `"Aangesloten, aansluiting voldoet aan wettelijke eisen"` (connected and compliant with the legal requirements for delivery). This is used to determine, for a given period, whether a connection was actually live.

- **[Confidential] Timeslices - PhysicalStatus.csv**
  Time-sliced history of the physical lifecycle status of each EAN connection, independent of whether energy is flowing. Columns: `EANUniqueIdentifier`, `StartDate`, `EndDate`, `Description`. Typical Dutch values: `"In aanleg"` (being constructed — the physical connection is being installed but not yet in service), `"In bedrijf"` (in service — physically operational).

- **[Confidential] Timeslices - Profile.csv**
  Time-sliced history of the standard load profile assigned to each EAN. In the Dutch small-consumer market, connections without interval metering are allocated using a standard profile code (`1A`, `1B`, `2A`, `3A`, `4A`, etc.) that describes the expected consumption pattern over the day/year. This code is used by balance-responsible parties to estimate hourly consumption. Columns: `EANUniqueIdentifier`, `StartDate`, `EndDate`, `Description` (the profile code).

- **[Confidential] Timeslices - ResidentialFunction.csv**
  Time-sliced history of the residential-function classification of the address behind each EAN (whether it is a dwelling, a non-residential unit in a residential building, etc.). This influences how VAT and certain regulated tariffs are applied. Columns: `EANUniqueIdentifier`, `StartDate`, `EndDate`, `Description` — a Dutch label such as `"Wel complex, geen verblijfsfunctie"` (part of a residential complex but without a residential/living function of its own).

- **[Confidential] Timeslices - UsageType.csv**
  Time-sliced history of the usage-type classification of each EAN. In the Dutch market, connections are split into `"Kleinverbruik"` (small consumers — households and small businesses below roughly 3×80A for electricity or 40 m³/h for gas, with regulated tariffs and standard profiles) and `"Grootverbruik"` (large consumers — above those thresholds, interval-metered, with negotiated tariffs). This classification drives which legal regime, tariff structure and metering rules apply. Columns: `EANUniqueIdentifier`, `StartDate`, `EndDate`, `Description`.

---

## 2. DevOps

### WikiPages/BITeam

- **[Confidential] Atrias - Allocation Data - BE.pdf**
  Architectural and functional overview of the integration that ingests allocation data published by the Belgian market clearing house (Atrias). Describes how XML allocation messages — both "big" and standard variants — land on an Azure Service Bus, are picked up by a pair of Azure Functions (a GETter that persists raw messages in blob storage, and a PROCESSor that parses them), and are written into the `BEGasAllocation` and `BEPowerAllocation` staging tables. Includes the full column list of both tables (contract/EAN reference, timeslot, volume, direction, flags) and explains the difference between the big-message and small-message flows.

- **[Confidential] DTN Data Integration.pdf**
  End-to-end description of the weather-data integration from the DTN service, which supplies solar and wind forecast/observation files consumed by the forecasting and imbalance processes. Covers the architectural diagram (external FTP → Azure Function App → blob data lake → SQL `EXTERNAL_PARTIES` schema), the seven file types recognised per region (`BE_forecast`, `BE_obs`, `FR_forecast`, `FR_Historical_Data`, `FR_obs`, `NL_forecast`, `xtremf_epx`), the five processing functions (one per file family plus a GET-files dispatcher), the naming convention the functions rely on, and the storage hierarchy / retention policy of the raw blob container.

- **[Confidential] Dynamic Profiles Data Integration.pdf**
  Describes how dynamic load-profile data for the Dutch market is pulled from the external profile API (one `STANDARD` and one `DYNAMIC` profile per NEDU profile category such as E1A/E1B/E2A/E3A/E4A) and landed as JSON in a blob container. Documents both the daily job (±60 business days around today, retrieved each business day) and the monthly job (last 10 business days of the previous month) together with the two Azure Functions that implement them (`Get-DynamicProfiles-Data` and `Get-DynamicProfiles-Monthly-Data`), the blob-path conventions used, and the retention policy.

- **[Confidential] NL MeterRead Validations report.pdf**
  Functional and technical specification for a BI report that flags meter-read anomalies on Dutch connections. Lists the validation rules applied at EAN level (missing period — `Missende periode`), at meter-read level (overlapping periods — `Overlap`, wrong starting meter — `Dubbel`) and at tariff-register level (movement inconsistent with direction — `Verschil in stand`, invalid LDN/ODN usage — `Fout verbruik`). Enumerates the report filters (Period, Supplier, Collective, Customer, WOZ, EAN, Product type, Metering method, Connection type, etc.) and the output columns (EAN, MeterNumber, MeterReadKey, start/end readings, usage, status). Also lists the two stored procedures that feed it and the QlikView app that renders it.

- **[Confidential] NL Refinitiv process.pdf**
  Documents the integration that pulls market prices from Refinitiv into the staging database. Two separate flows are described, each built as a four-step chain of Azure Functions (timer-triggered GET → queue-triggered extraction → queue-triggered blob save → queue-triggered SQL load): a **MidAsk** flow that runs Mon–Fri at 08:00 UTC and imports 67 ticker identifiers (futures / forwards on power and gas), and a **LocationalSpread** flow that runs Mon–Fri at 10:00 UTC and imports 8 identifiers. For each flow the document lists the source URL template, the identifiers involved, the request payload fields and the target table.

- **[Confidential] NL Solar and Wind Forecast.pdf**
  Functional specification of two BI dashboards that cross-reference forecasted solar / wind production with the associated downtimes and market bids, filtered on metering-point type (one dashboard for Solar, one for Wind). Lists the source tables used (forecast, near-real-time production, downtime data, bids), notes the 3-day look-back window that applies due to retention, and documents the refresh schedule (QVD reloads every 5 minutes, app reload every 5 minutes).

- **[Confidential] Notes on Position Overview.pdf**
  Business/functional notes explaining how the consolidated Position Overview is constructed from the different contract portfolios. Describes the contribution of each portfolio type — Supply, Sales, Physical, AretriSales, AretriPurchase, BRP France, ProfilingFactor — to the expected total consumption / generation, and the sign conventions used (positive vs negative side of the balance). Intended as a conceptual reference for anyone reading the Position Overview output.

- **[Confidential] Position Overview Gas NL.pdf**
  Single-page reference describing the structure of the Dutch gas Position Overview: the four portfolio sections shown side-by-side (two Supply portfolios and two Sales portfolios from two legal entities), what each section contributes (long vs short, market vs customer side), and how the final net position is derived.

- **[Confidential] Tennet - Allocation Data - NL.pdf**
  Describes the integration that consumes allocation data published by the Dutch TSO (Tennet) through the middleware service bus. Walks through the architecture (Service Bus topic subscriptions → Azure Function App → blob storage → `EXTERNAL_PARTIES` SQL schema), enumerates the message types handled (`BTennetMeasurementMessageReceived`, `BTennetNotificationSeriesMessageReceived`, `BTennetAcknowledgementMessageReceived`, etc.), shows a sample of the JSON body, and explains that each message type is stored in two forms: a flat table for simple messages and a pair of header/detail tables for series messages.

- **[Confidential] Tennet Data - Telemetry & Allocation.pdf**
  Describes the downstream ETL that turns the raw Tennet messages (stored under the `EXTERNAL_PARTIES` schema — see the companion document above) into two consumable datasets in the downstream data warehouse. Covers the **Telemetry** pipeline (loads `FactTelemetryTennet` daily before 9 AM Dutch time, filtered on `ProcessType IN ('N101','N141')`) and the **Allocation** pipeline (master pipeline triggering per-country sub-pipelines and stored procedures that load, de-duplicate and version the `TennetNLAllocation` table). Lists all the message types and pseudo-tables involved (MeasurementPoints, Readings, MeasurementRequests, NotificationMessages, VolumeRegisters, CounterParties, RevisionMessages, etc.).

- **[Confidential] UK Imbalance Calculations.pdf**
  Detailed specification of the UK Imbalance and Variance reports. Lists the monthly input file families (`ASP_Amendments_Invoice_Supporting_Information`, `COI_Core_Commodity_Individual_SMP_Supporting_Information`, `COM_Core_Commodity_Invoice_Supporting_Information`, `AML_Amendments_Invoice_SMP_Supporting_Information`) that arrive on FTP, then walks through the Azure Data Factory orchestration (per-commodity Get-Metadata → Filter → ForEach pipelines), the `Process_ADFFiles` stored procedure that stages them, the `SetSuccessStatusToFile` step and the SSIS master package that follows. Documents every measure of the **Imbalance Report** (Commodity, Reconciliation, Consumption, OverallImbalance, SAP, Imbalance) and the **Variance Report** (MPR, LastBillToDate, DateOfLastRec, SalesVolume, BillableQuantity, DeemedQuantity, VarianceQuantity, EnergyVariance, Total, Variance) with the SQL-level formula behind each one.

- **[Confidential] UK Overview.pdf**
  Overview of the data model behind the UK billing / CIS system (Junifer). Describes the **Customer/Account** layer (account-type stamps such as active, lapsed, in-collections, pending-renewal, and the address-history / contact tables with `startDate` / `endDate` validity), the **Contract** layer (commercial contract, authorised trader, prospect/quote data, payment preference data), the **Quote Pricing** sub-model (links quote + MeterPoint + price matrix), and the **MeterPoint** layer (MPRN/MPAN details, meter data, AQ/SOQ history, settlement class, LDZ, Exit Zone, AUFDASF/NCF/CV factors used for forecasting). Read as the entry-point reference when mapping Junifer data into the warehouse.

### WikiPages/DevTeam

- **[Confidential] Atrias allocation.pdf**
  Deep-dive technical integration spec for the Belgian market clearing system (Atrias CMS / UMIG 6). Covers the referenced XSD schema files, the external links, the full infrastructure setup (site-to-site VPN, QWAC certificate, SFTP access and firewall rules, per-environment breakdown of DEV / ACC / PRE-PROD / PROD, connectivity tests — port, SFTP, IsAvailable, GetMessage — and the UMIG 6 transition schedule). Also documents the CMS B2B communication patterns (MEP 1 sync request/response, MEP 2 async request/response, MEP 3 async event), the OSI application / session layers, the Web Service protocols, idempotency and reliable polling, the SFTP directory layout and naming conventions, and the full getter/setter allocation-message workflows with their happy-path and exception-path flows.

- **[Confidential] AWP - Functional overview.pdf**
  End-user functional manual for the Average Weighted Price calculation application used by the BRP team to price the energy delivered to suppliers. Walks through every screen: Supply contracts (overview, CSV import and validation rules for each required field), Propositions (create/edit/delete, historical import, recurring & manual market prices import, export), Price discounts (CRUD with threshold conditions), Auction prices, and the AWP Calculation itself (invoice generation, invoice details and exports, and the calculation methodology — prerequisites checks, legacy data, volumes calculation, LEBA / APX weighted + unweighted price matching, results computation). Ends with the System Events dashboard and notification subscriptions.

- **[Confidential] AWP - Technical overview.pdf**
  Technical companion to the AWP functional overview. Covers the architecture (React front-end + API + SQL; EDI integration for supply contracts and CSV uploads; market-price feeds from LEBA/APX), the full database model (DBML code and entity diagram for Supplier / Proposition / Imports / PriceDiscounts / ExpirationDates / MarketPrices / CalendarDates tables), the connection-check logic run before each calculation, and the step-by-step pipeline: supply contract upload, proposition adjustment, validations, volumes calculation, Pricing matches, results matching, result storage.

- **[Confidential] Broker commissions functionality overview.pdf**
  Functional specification for the Broker Commissions application that tracks the brokers, their contracts with the supplier, and the commissions owed to them per end-client contract. Documents the settings page (thresholds, multi-select filters, auto-refresh), broker management (add, edit, change status, bulk upload, notes and attachments), broker contracts (CRUD), processing of supplier snapshots, the four-eyes approval flow (broker contract, contract extension, commission calculation) and, most extensively, the commission-calculation engine — per contract, per energy unit (unique, range, by time of consumption), matrix, payment terms, bonuses — plus the recalculation logic for cancelled and open-ended contracts, the upload of already-paid commissions and the clawback handling for early-termination cases.

- **[Confidential] BRP functional overview.pdf**
  Top-level functional manual for the core Balance Responsible Party application that manages energy trading and balancing for power and gas across NL, BE and FR. Covers Administration (org units, roles, users, languages, audit logs, subscriptions, visual settings and the per-module application settings — Appearance, User management, Security, Invoice, Legacy management, Bidding management, FTP, Temperature data), Allocation (imports), BRP configuration (program time units, load profiles, profile coefficients and fractions, theta), Volume calculation, Pricing (propositions, settlement-price retrieval/processing/upload for BRPNL/BRPBE via ICE-ENDEX and BRPFR via EEX, EPEX Spot upload, historical spot prices, 48-month-ahead pricing, Price Forward Curve calculation, fixed vs variable price calculation), Invoicing, Forecast (forecast overview, supplier snapshots, bids, event calendars, national balancing factor, wind/solar production forecast and provider comparisons), Deals (overview, counterparties, intraday), Nominations, Weather (normal temperature, weather stations, realized / forecast temperatures, TAF calculation, A/B factors), System Events and T-Prognose.

- **[Confidential] Downtimes Portal architectural overview.pdf**
  Architectural description of the Downtimes Portal — a web application where customers with large-scale connections register planned outages so that their consumption forecast can be adjusted. Covers the output-style deployment diagram (user browser → React SPA → .NET Web API behind App Service → SQL databases for Downtimes and Production Forecast, with Managed Identity-based access and Key Vault for secrets), the BFF layer, the application projects (API, Application, Infrastructure, Domain, Shared, Tests), authentication and authorisation model (Azure AD), and the Azure resource inventory — App Service, Key Vault, static site, SQL databases, Service Bus, application domains.

- **[Confidential] Downtimes Portal functional overview.pdf**
  End-user functional description of the Downtimes Portal. Describes the "Manage powerplants for your connections" screen (listing and filtering the user's EANs, map view, type/operator/name filters), registration of a new downtime (date range, capacity, reason, big storm flag), updating an existing downtime, cancelling, the daily confirmation email, the validation rules enforced when creating a downtime and the production-forecast overview page that shows the downtime's effect.

- **[Confidential] ETRM Nomination Integration.pdf**
  Integration spec for the ETRM (Energy Trading & Risk Management) system, used as the authoritative source for nomination data sent onward to TenneT. Lists the reference files (Nomination Result report, Daily Realisation Summary, Import Locations, Swap/AHAD report samples) and the REST endpoints used — authentication (Bearer token, URL-encoded credentials), GET report parameters, GET report data — together with example request/response bodies in Postman form.

- **[Confidential] eZ Integration.pdf**
  Integration spec for the eZ-operations system (third-party ETRM used as the channel to submit nominations to TenneT for the NL market). Documents the demo API, the reference mapping spreadsheets (EAN mappings, decimal segments, nomination schedule, counterparty timeseries), authentication (XML POST `<userCredentials>` → Bearer token, refresh), Timeseries submission (PUT EditTimeSeries with XML `<decimalSegment>` / `<dateRange>` / `<value>` blocks), and the Nomination submission + status retrieval endpoints.

- **[Confidential] Invoice configurations.pdf**
  Reference manual for everything that has to be configured before invoices can be generated. Documents the Administration → Settings tabs (general invoicing settings, invoice-number settings for last-used numbers, VAT settings), the Invoice permissions model, every nightly data-transfer job from the master billing system (invoice clients, contract details, networking cost, meter reads, meters, deleted meter reads, profile fractions, power / LEBA gas auctions, imbalance prices, deleted entities, debtor contact person, contract assignment, connection invoice type, deals, prices, connection status), yearly taxes, grid operators, exit and connection cost configuration, the Price Elements screen (overview, add — 4-step wizard selecting price template → invoice clients → client settings → price-template card — with feed-in / usage / additional-cost templates), invoiced consumption upload, edit-already-invoiced consumption, paused invoice generation, SDE settlement configurations, audit logs and multi-user actions.

- **[Confidential] Invoice documents.pdf**
  Specification of the invoice document formats that are produced for each approved invoice. Lists the inputs the documents consume (debtor & address, customer details, contract details, connection, EAN, meter, price elements, per-period consumption and calculated amounts, deals data, totals, stand/capacity rows, settlement amounts, reduction amounts, SDE) and the content rules per output format: PDF (language-driven, per-client template), XML-Exact (import into the finance system, including CreditNote variant for credit invoices), XML-UBL (business-to-government format, with property-mapping table for address/booking/delivery/postal/other), and X4L settle billing.

- **[Confidential] Invoice management (screens).pdf**
  Functional spec for the Invoice Management module — the UI used by billing agents to review, approve and reject the invoices produced by the generation process. Documents the Invoice pending approval screen, the 2.1 Invoice pending approval view (decline, collective name, holds, unhold invoices), the 2.2 Approve, Decline, Hold, Unhold invoices flow (with 4-eyes), credit invoices (approve, pay, correct, decline, decline back to approval), period invoices, 3 Invoice overview (3.1 Invoice overview grid, 3.2 Approve declined invoices report, 3.4 Credit invoice, 3.5 Repaid invoice, 3.6 Error-verifying-invoice process after approval, 3.7 Send out invoices to client when Exact returns 'already...'), 3.8 Mark invoices as Printed, 3.9 We've selected invoice PDF into 1 and download, 3.10 Entire tax-&-savings difference report.

- **[Confidential] NominationToolFunctionalOverview.pdf**
  Functional overview of the Nomination Tool — an operator UI that lets users manage the scheduled events driving the event-driven nomination flow. Describes the Microsoft SSO login, the Nominations overview page and its table (event first column with category label, action column with radio per row to enable/disable or trigger the send-to-eZ step, status column, next scheduled time, cron schedule) and the edit-event-schedule popup for modifying a nomination's cron schedule.

- **[Confidential] Payt invoice data.pdf**
  Specification of the Payt integration (payment / receivables platform). Lists the record types retrieved from the Payt API — Invoices, Notes, Notifications, Payment plans, Tasks — the API authorization mechanism and endpoints, and the Middleware implementation: scheduled data-retrieval flow, overlapping-window tracking and sync-timestamp handling, and the provisioning of the resulting data to the internal BI consumer. Ends with the CI/CD pipeline for the Invoice-Release and the alerting setup.

- **[Confidential] Period and Final bill generation.pdf**
  End-to-end specification of the period-bill and final-bill generation flow. Covers the JSON "flatline" import, the semi-automatic invoice generation path (add new EANs, generate for all EANs, generate for one EAN, retry a failed EAN) and the manual one (import period/final bill JSON/flatline, generate period bill on a list of EANs, with a full breakdown of the invoice-line calculation rules for each price element: standing charge (proposition), delivery (proposition), feed-in (proposition), networking cost, SDE tax, connection fee, tariff per day — and their equivalents on the standing-charge-per-tariff / delivery side), the global network-cost logic, automatic period-/final-bill flow, invoice generation and error logs.

- **[Confidential] Quote tool functionality overview.pdf**
  Functional overview of the self-service Quote tool for small-business customers requesting electricity/gas pricing. Describes the four-step wizard (Step 1 Company details with KVK lookup, Step 2 Energy requirements with EAN search, consumption fields, dual-rate meter, solar panel flag and validation ranges, Step 3 Review Quotes, Step 4 Confirm with T&C and PDF), the calculation engine (contract tariffs read from a price Excel, energy taxes retrieved from EMS) and the Welcome email + PDF contract that is generated.

- **[Confidential] Sector calendar.pdf**
  Technical spec for the sector-calendar service — the component that extends the standard working-day/weekend/holiday calendar with market-specific "legal holiday" and "bridge day" dates that are required to correctly validate Tennet messages. Describes the `CalendarEventDate` SQL entity, the `CalendarEventType` enum (`LegalHoliday = 1`, `BridgeDay = 2`), the HTTP-triggered functions exposed (AddNewCalendarEventDate, DeleteCalendarEventDates), sample JSON payloads and the published Bijlage B / Sektorkalender annex the dates come from.

- **[Confidential] Settlement prices.pdf**
  Describes the ETRM-side settlement-price integration. Covers the retrieval of settlement prices from three providers — EEX, PowerNext, IceEndex — via CE Broker and an SFTP drop, the daily/monthly/quarterly/yearly processing flow, the input structure (subfolder per provider), the output (SFTP drop for BRP) and the mapping between external product codes and BRP commodity curves. Ends with a support playbook for the "Missing EEX price curve files" scenario (DataHub inspection, manual trigger via ETRM UI).

- **[Confidential] Sia Temperature API.pdf**
  Integration notes for the temperature API provided by Sia Partners, used for the day-ahead Temperature Adjustment Factor for the French BRP. Lists the contact persons, versioning/endpoints (`/prev3/{wmo_code}/{startdatetime_utc}` for temperature forecasts, `/stations` for listing stations) and the base-URL configuration in the BRP app settings, Key Vault token setup, and describes the two main operations: Stations (retrieves all measurement stations in France with weighing factors, ID, name, insee/lat/lon coordinates, postal code, wmo_code) and Temperature forecast (3-day forecast per station from the given start UTC hour).

- **[Confidential] Signals.pdf**
  Specification of the "signals" produced daily by the pre-invoice quality-check process — each signal flags a reason why an invoice cannot be auto-generated for a given EAN and month. Lists all monthly-invoice signal families (incomplete NAC data, missing pricing, missing ODE / Energy tax tariffs, wrong connection/contract status, meter-read issues — not available / duplicated / overlapping, consumption differs from actual meter reads, previous-period invoice check, missing payment terms / payment-term ledger code, missing grid operator data, exit and connection-cost checks, tax-reduction checks, credit-invoice-exception code), the SDE signals (all months have invoices, meter reads, client/debtor/EAN information), and the period-bill signals (meter reads, client/debtor/EAN/contract checks, price checks, master-data checks, existing-invoice check).

- **[Confidential] Tennet allocation.pdf**
  Comprehensive spec for the allocation integration with the Dutch TSO (TenneT) for the NL market. References every Measurements-Allocation technical guide and XSD (the EDSN `MeasurementSeriesNotification` and `VolumeNotification` schemas, N10 / N20 / N41 / N90 / N91 / N101 / N111 / N121 / N131 / N132 / N141 / N142 / N151 message-type XSDs). Describes the overall architecture (Allocation Revision Tool SPA → App Gateway + WAF → Azure Function → SQL + blob storage → data warehouse), the every-20-minutes polling, and documents each message type in detail — N10/N20 Daily/Periodic Allocation Point Metering Data, N41 Periodic Allocation Point Measurement Data, N90 Complaint after verification, N91 Complaint after completeness check, N101/N131/N141 Daily Allocation data per individual allocation point / grid loss / dimensioned, N102/N111/N121/N132/N142/N151 Daily report aggregated allocation TMT / SMA / PRF / grid loss / dimensioned / residual volume correction — with SOAP envelope schematics (Correlation ID, technical ID, EAN13 sender / receiver), the information model, dependency table and validation rules. Ends with the qualification / testing notes and a TQF env-update history.

- **[Confidential] Tennet Imbalance data.pdf**
  Spec for the parallel TenneT Imbalance-data integration (Aggregated Imbalance and Acknowledgement messages, delivered via the MMC Hub). Covers the input files (per-message implementation guides, the NL Imbalance / Acknowledgement Implementation Guides and code-list XSDs), the B2B communication (SOAP envelope header details, message polling, endpoints, retention period, IP whitelisting & certificates), the message models (aggregated imbalance message — information model, time-series by business type, dependency table, validation rules; acknowledgement model), qualification notes, and the implementation details — architecture, database + blob storage, data access layer, message polling/processing/validation, acknowledgement sending, data provisioning to consumer, reprocessing, custom functions, CI/CD and alerting.

- **[Confidential] Validation rules.pdf**
  List of all the validation rules applied when generating an invoice — only if every rule passes is an invoice accepted. Examples: limit for standard amounts (per Connection vs per Collective), max consumption thresholds, EAN limits per collective, difference-between-meter-reads threshold, user-approval-required flags for exceptional cases, invoice amount too high / too low, duplicate meter reads, deviation in S/V signals, zero-consumption invoices for monthly/flat/biannual clients, calculated amount on Connection-level lines, amount on Delivery-lines-per-meter-read, WOZ-not-fully-invoiced and more than thirty additional checks identified by unique TB rule IDs.

---

## 3. ERPSQLServer

- **[Confidential] ConnectionTypes.csv**
  Tiny lookup table enumerating the physical purpose of a connection. Columns: `ConnectionTypeId`, `Description` (English code), `PresentationDescription` (Dutch label shown in the UI), `UserNameModified`, `TransStartDate`. Values: `1` CHP / WKK, `2` Biomass / Biomassa, `3` WindTurbine / Windmolen, `4` Lighting / Belichting, `5` OwnUsage / Eigen verbr., `6` FlexibleUsage / Flex. Verm., `100` MainConnection / Hoofdaansluiting.

- **[Confidential] OrganizationTypes.csv**
  Tiny lookup table enumerating the kinds of legal/commercial party the system knows about. Columns: `OrganizationTypeId`, `Description`, `PresentationDescription`, `UserNameModified`, `TransStartDate`. Values: `1` HeadOrganization / Hoofdorganisatie, `2` Customer / Klant, `3` DealCounterparty, `5` Collective / Collectief, `6` Company, `7` Broker.

- **[Confidential] ProductTypes.csv**
  Tiny lookup table enumerating the energy commodities sold. Columns: `ProductId`, `Description`, `PresentationDescription`, `PortaalTypeCode` (= `ELK` for both rows), `UserNameModified`, `TransStartDate`. Values: `1` Electricity / Elektriciteit, `2` Gas.

- **[Confidential] Organizations.csv**
  Master table of every organisation the system tracks — customers, collectives, deal counterparties, brokers and the head organisations that own them. Columns: `OrganizationId`, `OrganizationTypeId` (FK to `OrganizationTypes`), `DebtorReference` (external accounting key, often `NULL`), `Name` (pseudonymised hash), `UserNameModified`, `TransStartDate` (audit stamp).

- **[Confidential] LastConnectionContacts.csv**
  Convenience view listing only the most-recent contact entry per connection. Columns: `connectionid`, `contactid`, `contactdate`, `subject` (General, Cancellation, MeasureData, Modification…), `report` (pseudonymised free-text report hash), `Volgorde` (sequence/rank, typically `1`).

- **[Confidential] Contracts.csv**
  Contract master data — one row per contract record, including both long-running customer contracts and their per-period child contracts. Columns: `ContractId`, `ContractType` (`Customer` or `Period`), `ContractReference`, `ProductId` (FK to `ProductTypes`), `StartDate`, `EndDate` (`9999-12-31` for open-ended), `UserComment`, `UserNameModified`, `TransStartDate`, `CurrentAgreedAmount`.

- **[Confidential] Connections.csv**
  Master table of every energy connection (one row per physical delivery point). Columns: `ConnectionId`, `EAN` (18-digit connection code, pseudonymised to a hash), `ProductType` (Electricity/Gas), `DeliveryType` (`LDN` = Levering / supply, `ODN` = Onttrekking / withdrawal, `NA`), `ConnectionTypeId` (FK to `ConnectionTypes`), `Description`, `ClientReference`, `ExternalReference`, `UserNameModified`, `TransStartDate`.

- **[Confidential] ConnectionContacts.csv**
  Denormalised join between `Connections` and their contact-history records. Left-hand columns repeat the connection (ConnectionId, EAN, ProductType, DeliveryType, ConnectionTypeId, Description, ClientReference, ExternalReference), and the right-hand side adds one contact row per occurrence: `ConnectionContactId`, `ContactId`, `ValidStartDate`/`ValidEndDate`, `ContactDate`, `UserName`, `ContactPerson`, `ContactPersonType` (Person/PersonId), `Subject` (General, Cancellation, Modification, MeasureData, …), `ProductId`, `Report` (hashed free-text note), audit fields. Effectively the full interaction history per connection.

- **[Confidential] Contract-Customer-Connection-BrokerDebtor.csv**
  Flattened "one-row-per-contract-per-connection" join used for reporting and lookups. Columns: `EAN`, `ConnectionId`, `ContractID`, `ContractNumber`, `StartDate`, `EndDate`, `Market` (Electricity/Gas), `CustomerNumber`, `CustomerName`, `BrokerNumber`, `BrokerName`, `DebtorNumber`, `DebtorName`. Each row answers "which customer was behind this EAN under this contract, through which broker, billed to which debtor, between these dates". Names are pseudonymised hashes.

- **[Confidential] OrganizationContacts.csv**
  Denormalised join between `Organizations` and their contact-history records — same layout as `ConnectionContacts.csv` but scoped at the organisation (customer / counterparty / broker) level. Left-hand columns repeat the organisation (OrganizationId, OrganizationTypeId, DebtorReference, Name), right-hand columns carry the contact details (OrganizationContactId, ContactId, ValidStart/End, ContactDate, UserName, ContactPerson, ContactPersonType, Subject, ProductId, Report, audit stamps).

- **[Confidential] [ValueAQuery] ASU001.csv**
  Annual Standard Usage per connection — the official SJV (Standaard Jaar Verbruik) values used by the grid, one row per validity period per EAN. Columns: `ConnectionAnnualStandardUsageId`, `ConnectionId`, `EAN`, `ValidStartDate`, `ValidEndDate`, `EAEnergyConsumptionNettedOffPeak`, `EAEnergyConsumptionNettedPeak`, `EAEnergyProductionNettedOffPeak`, `EAEnergyProductionNettedPeak`, `AnnualStandardUsageDate`. The four numeric columns split low/high tariff on the consumption side and low/high tariff on the production (feed-in) side.

- **[Confidential] [ValueAQuery] DQE - Captars.csv**
  Connection-level network-operator tariff history — links each EAN to its Captar (tariff classification) code and to the grid operator (`NB` — Netbeheerder) tariff in force for each sub-period. Columns: `CaptarContractId`, `ConnectionId`, `EAN`, `StartDate`, `EndDate`, `ValidStartDate`, `ValidEndDate`, `NBCode`, `captarcode`, `CaptarCode`, `EanCaptarCode`, `FysicalCapicity`, `FysicalStatus` / `FysicalStatusDescription` (e.g. "In bedrijf"), `ContractModel` / `ContractModelDescription` (e.g. "Leveranciersmodel"), `VatCategoryId`, `CaptarNBId`, `CaptarId`, `NBId`, a set of `Amount{Year,Day,APYear,APMonth,APDay}` and `Amount{Captar,ConnectionService,MeterRent,SystemService,FixedCharge}` tariff-component columns, `VATpc`, and the Captar/NB validity stamps.

- **[Confidential] [ValueAQuery] CPY001.csv**
  Time-sliced generic property store for connections (one row per property per validity period). Columns: `ConnectionConnectionPropertyId`, `ConnectionId`, `EAN`, `ValidStartDate`, `ValidEndDate`, `ConnectionPropertyValue`, `ConnectionPropertyTypeId`, `Description` (property type key — e.g. `StandardCost`), `PresentationDescription` (Dutch label — e.g. "Standaard kostprijs"), `ConnectionPropertyGroup` (property family — e.g. `CHP`).

- **[Confidential] ConnectionMeterReads.csv**
  Aggregated meter-reading records per connection — one row per reading period. Columns: `UsageID`, `ConnectionId`, `EAN`, `MeterID`, `ReadingDate`, `StartDate`, `EndDate`, `MeterType` (Gas/Electricity), `UsageSource` (typically `ECH`), `UsageType` (`CorrectedUsageGas` etc.), `Quality`, `Consumption` (numeric, unit matches `Unit`), `Position`, `PreviousPosition`, `Direction` (Consumption / Production), `Unit` (`m3` for gas, `kWh` for electricity).

- **[Confidential] [ValueAQuery] DQE - Prijzen v5 met Organization.csv**
  Full price-component history per connection/contract, joined with the organisation that owns the proposition. Columns: `ConnectionId`, `EAN`, `ProductType`, `ContractId`, `ContractReference`, `Description`, `PresentationDescription` (e.g. "WKK Variabele Prijs"), `Price`, `PriceComponentId`, `ComponentDescription` (e.g. `ID_REBElectricity`, `ID_TaxCreditElectricity`), `ComponentPresentationDescription` (Dutch — e.g. "REB Elektriciteit", "Heffingskorting Elektriciteit"), `PriceComponentPriceId`, `StartDate`, `EndDate`, `Type` (`Propositie`), `Name` (supplying organisation). Each row is a tariff/tax line with its validity period, so the file together reconstructs the full price book applied to each contract across time.

- **[Confidential] [ValueAQuery] ERPMRE.csv**
  The detailed meter-read-event fact table — one row per metered quantity per read, covering every connection and every period in scope. Columns: `ConnectionId`, `UsageId`, `MeterReadingId`, `MeterPositionId`, `EAN`, `MeterNumber` (pseudonymised), `MeterType` (Electricity/Gas), `Factor`, `Amount` (the measured value, can be negative for production), `usg_quality` (Estimated / Measured / Customer), `UsageType` (`UsageLow`, `UsageHigh`, `kWmax`, …), `StartDate`, `EndDate`, `sts_description` / `sts_presentation_description` / `StatusDetails`, `Position`, `TimeFrame`, `pos_quality`, `QuantityDate`, `QuantityDateType`, `src_description` / `src_presentation_description` (source system — e.g. `ECH`). This is the largest table in the folder and is the source of record for both normal consumption and the peak-demand (`kWmax`) values.
