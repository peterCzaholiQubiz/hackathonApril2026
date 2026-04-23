# ADR-001: .NET 10 ASP.NET Core as Backend Framework

## Status

Accepted

## Date

2026-04-10

## Context

The Customer Portfolio Thermometer requires a backend that can:

- Import data from a CRM source (database connection or CSV files)
- Execute deterministic risk scoring rules over hundreds of customers
- Call the Claude API with controlled concurrency and retry logic
- Serve a REST API to an Angular frontend
- Run inside a Docker container on a single laptop
- Be developed within a hackathon timeline (approximately 8 days)

The team has .NET experience and the source CRM (DVEP) uses a relational database that .NET data access libraries handle well. The hackathon evaluation values a working end-to-end demonstration, not language novelty.

## Decision

Use .NET 10 with ASP.NET Core Web API as the backend framework.

## Consequences

### Positive

- **Mature ecosystem**: Entity Framework Core with Npgsql provides first-class PostgreSQL support including migrations, LINQ queries, and bulk operations
- **Strong typing**: C# record types and enums map cleanly to the domain model (risk scores, heat levels, action types) and reduce runtime errors
- **Built-in concurrency primitives**: SemaphoreSlim and HttpClient pooling handle the Claude API throttling requirement naturally
- **Docker support**: Official .NET SDK and runtime images are well-maintained and produce small containers with `dotnet publish` trimming
- **Clean Architecture fit**: .NET dependency injection, interface-based services, and project separation (Api/Core/Infrastructure) align with the planned layered architecture
- **Team velocity**: Existing .NET competence eliminates ramp-up time, critical in a hackathon

### Negative

- **Cold start**: .NET containers have a heavier cold start than Node.js or Go, though this is irrelevant for a long-running demo server
- **Binary size**: The published container image is larger than a Go or Rust binary, though multi-stage Docker builds mitigate this
- **Less common in hackathon contexts**: Judges more accustomed to Python/Node may need brief orientation to the project structure

### Alternatives Considered

| Alternative | Why Not Chosen |
|-------------|----------------|
| **Python (FastAPI)** | Strong for AI workloads, but weaker typing and ORM story for complex relational queries. Team has less Python experience. |
| **Node.js (Express/Fastify)** | Fast to scaffold, but TypeScript ORM options (Prisma, TypeORM) are less mature than EF Core for migration management. Concurrency control for API batching is less ergonomic. |
| **Go (Gin/Echo)** | Excellent for performance and small binaries, but the team lacks Go experience. ORM ecosystem is less mature. Hackathon timeline does not permit language ramp-up. |
| **Java (Spring Boot)** | Comparable ecosystem maturity, but heavier boilerplate and slower iteration cycle. No team advantage over .NET. |
