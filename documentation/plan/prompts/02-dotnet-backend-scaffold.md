# Prompt 02 — .NET 10 Backend Scaffold

**Agent**: `general-purpose`  
**Phase**: 1 — Foundation  
**Status**: DONE

---

Create the .NET 10 solution for the Customer Portfolio Thermometer backend.

Read documentation/plan.md for the full project structure.

1. Create backend/PortfolioThermometer.sln
2. Create 4 projects:
   - PortfolioThermometer.Api (ASP.NET Core Web API, .NET 10)
   - PortfolioThermometer.Core (class library, NO external dependencies)
   - PortfolioThermometer.Infrastructure (class library, references Core)
   - PortfolioThermometer.Seeder (console app)

3. In Core: create all domain models, enums, and interfaces as specified in the plan:
   - Models: Customer, Contract, Invoice, Payment, Complaint, Interaction,
     RiskScore, RiskExplanation, SuggestedAction, PortfolioSnapshot
   - Enums: HeatLevel, RiskType, ActionType
   - Interfaces: ICrmImportService, IRiskScoringEngine, IClaudeExplanationService,
     IPortfolioAggregationService, ICustomerRepository

4. In Infrastructure: set up AppDbContext with Npgsql EF Core, all entity configurations
   matching the schema in database/init.sql

5. In Api: Program.cs with DI wiring, CORS for localhost:4200, global error handler middleware,
   consistent API response envelope { success, data, error, meta }

6. Add NuGet packages:
   - Npgsql.EntityFrameworkCore.PostgreSQL
   - Bogus (Seeder project only)
   - xunit + Moq + FluentAssertions (test projects)

7. Create a backend/Dockerfile for multi-stage build (.NET 10 SDK -> runtime)

8. Create an initial EF Core migration.

Reference: documentation/plan.md (Backend section), database/init.sql
