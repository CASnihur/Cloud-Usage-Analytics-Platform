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

The container exposes SQL Server only on `127.0.0.1:1433` and persists its data in the named Docker volume `cloud-usage-sqlserver-data`. The `.env` file is ignored by Git and must never be committed.

Configure the API's local connection string with .NET User Secrets. Replace `<password-from-dotenv>` with the value you chose in `.env`; do not include angle brackets.

```powershell
dotnet user-secrets set --project src/CloudUsage.Api "ConnectionStrings:UsageAnalyticsDatabase" "Server=127.0.0.1,1433;Database=CloudUsageAnalytics;User ID=sa;Password=<password-from-dotenv>;Encrypt=True;TrustServerCertificate=True"
```

Restore the local EF Core CLI tool before creating or applying migrations:

```powershell
dotnet tool restore
```

Apply all pending EF Core migrations to the local database:

```powershell
dotnet tool run dotnet-ef database update --project src/CloudUsage.Api --startup-project src/CloudUsage.Api
```

The local container uses a self-signed certificate, so `TrustServerCertificate=True` is limited to development. Production will use Azure SQL with proper certificate validation and managed secret configuration.

Build and test the solution:

```powershell
dotnet restore CloudUsageAnalytics.slnx
dotnet build CloudUsageAnalytics.slnx --no-restore
dotnet test CloudUsageAnalytics.slnx --no-build --no-restore
```

Run the API:

```powershell
dotnet run --project src/CloudUsage.Api
```

Visit `http://localhost:5073/health` to verify the API is running.

## Single-event ingestion and integration tests

The single-event endpoint is `POST /api/usage-events`. See the API project's `.http` file for a sample. A durable insert returns `201 Created`; invalid input returns `400` problem details, and an existing event ID returns `409 Conflict`. The response confirms raw storage; analytics processing has not occurred yet. There is no event retrieval endpoint yet, so the response does not advertise a Location URL.

The endpoint accepts optional object-valued `properties`, enforces string limits matching the schema, and limits the request body to 64 KiB. Receipt time is server-generated; occurrence timestamps are normalized to UTC. Event-type catalogs and business-specific timestamp limits are deferred.

Ordinary tests exercise HTTP binding using a fake service. To also run the SQL Server integration test, start Docker SQL Server and configure the API User Secret above, then run:

```powershell
$env:CLOUD_USAGE_SQL_TESTS = '1'
dotnet test CloudUsageAnalytics.slnx
Remove-Item Env:CLOUD_USAGE_SQL_TESTS
```

The SQL test requires permission to create/drop databases. It migrates a unique `CloudUsageTests_<guid>` database and removes it afterward; it never migrates or clears the application database. It covers persistence, sequential duplicates, a forced concurrent insert race, unexpected constraint failures, cancellation, and HTTP-to-SQL ingestion. If a test process is killed, a test database may require manual cleanup.

### Batch ingestion

`POST /api/usage-events/batch` accepts an object with an `events` array of 1–100 items. Each item is independently parsed and validated using the single-event rules. A processed batch returns HTTP 200 with ordered `results`; each includes its zero-based `index` and item `status` (201 created, 400 invalid, or 409 duplicate). Created items include `event` details; invalid items include `errors`; duplicates include `detail`. Unused fields are null.

Malformed JSON or an invalid envelope returns overall 400. Processing is sequential and non-atomic: each successful insert is committed independently. Unexpected failures stop processing and return overall 500 without accumulated item results; earlier inserts remain stored. On retry, those IDs return 409. Cancellation or connection loss can also leave partially stored batches. Keep event IDs stable across retries.

The `.http` file includes a mixed-batch example. SQL integration coverage verifies mixed outcomes, persisted row counts, and retry behavior. Bulk SQL insertion and throughput tuning are deferred; this endpoint still performs per-event database operations. The 100-item cap is not a per-item byte limit; the batch currently uses the host's request-body limit.

## Future iterations

Once the MVP is working end-to-end, potential extensions include scheduled transformations, Azure Data Factory or dbt, richer analytics such as retention or cohorts, authentication, and queue-based ingestion.
