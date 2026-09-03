# Cloud Usage Analytics Platform

A portfolio project that demonstrates a small, production-oriented analytics platform for product-usage data.

The application will ingest synthetic product-usage events, preserve them as raw data, transform them into an analytics-friendly dimensional model, and present usage metrics in a web dashboard. It is being built incrementally to demonstrate practical backend engineering, data modeling, testing, observability, and Azure deployment.

## Problem

Product teams need reliable answers to questions such as:

- How many people used the product each day?
- Which products or features generate the most activity?
- Is usage increasing, decreasing, or changing over time?

Operational event payloads are useful for ingestion and traceability, but are not ideal for dashboard queries. This project separates those needs by storing raw events first and transforming them into a simple analytics model.

## MVP scope

The first end-to-end version will:

1. Accept synthetic product-usage events through an HTTP API.
2. Store raw events safely and prevent duplicate ingestion.
3. Transform events into these analytics tables:
   - `fact_usage_event`
   - `dim_user`
   - `dim_product`
   - `dim_date`
4. Expose dashboard metrics for daily active users, events by product, and usage trends.
5. Display the metrics in an Angular dashboard.
6. Add automated tests, structured logging, error handling, and health checks.
7. Deploy a working version to Azure.

## Planned architecture

```text
Synthetic event source
        |
        v
ASP.NET Core Web API
        |
        +--> Raw event storage
        |
        +--> Transformation process --> Dimensional analytics tables
                                              |
                                              v
                                      Dashboard metric endpoints
                                              |
                                              v
                                      Angular web dashboard
```

The MVP starts as a modular monolith: one deployable backend with clear ingestion, transformation, and analytics-query responsibilities. This deliberately avoids premature distributed-system complexity while leaving natural extraction boundaries for later iterations.

## Technology stack

| Area | Technology |
| --- | --- |
| Backend | C# / ASP.NET Core on .NET 10 |
| Database | SQL Server locally, Azure SQL planned for deployment |
| Frontend | TypeScript / Angular |
| Data processing | .NET and SQL for the MVP |
| Observability | Structured logging, health checks, and practical metrics/tracing |
| Testing | xUnit unit tests and integration tests |
| Delivery | Git, GitHub Actions, Azure |

## Current status

**Phase 1 — Backend foundation:** in progress.

The repository currently contains a .NET 10 ASP.NET Core API project, an xUnit test project, and a controller-based `GET /health` endpoint.

## Local development

Prerequisites for the current backend scaffold:

- .NET 10 SDK
- Docker Desktop, using Linux containers, for the local SQL Server environment

### Local SQL Server

The project uses SQL Server in Docker for reproducible local development. Copy `.env.example` to `.env`, choose a strong local `MSSQL_SA_PASSWORD`, then start the database:

```powershell
docker compose up -d
docker compose ps
```

The container exposes SQL Server only on `localhost:1433` and persists its data in the named Docker volume `cloud-usage-sqlserver-data`. The `.env` file is ignored by Git and must never be committed.

Build and test the solution:

```powershell
dotnet restore CloudUsageAnalytics.slnx --source https://api.nuget.org/v3/index.json
dotnet build CloudUsageAnalytics.slnx --no-restore
dotnet test CloudUsageAnalytics.slnx --no-build --no-restore
```

Run the API:

```powershell
dotnet run --project src/CloudUsage.Api
```

Visit `http://localhost:5073/health` to verify the API is running.

## Future iterations

Once the MVP is working end-to-end, potential extensions include scheduled transformations, Azure Data Factory or dbt, richer analytics such as retention or cohorts, authentication, and queue-based ingestion.
