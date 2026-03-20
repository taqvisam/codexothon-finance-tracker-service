# codexothon-finance-tracker-service

Backend API for Personal Finance Tracker.

## Stack
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- JWT auth + refresh tokens
- Background worker for recurring transactions

## Deploy with Podman
1. From this repository root, create env file:
   - `Copy-Item .env.example .env -Force`
2. Build and run:
   - `podman compose up --build -d`
3. Verify health:
   - `curl http://localhost:5000/health`

Expected JSON:
```json
{
  "status": "Healthy",
  "timestamp": "2026-03-20T10:30:00Z",
  "services": {
    "api": "Healthy",
    "database": "Healthy"
  }
}
```

4. Stop:
   - `podman compose down`

## Deploy without Podman
1. Ensure PostgreSQL is running and reachable.
2. Update `src/PersonalFinanceTracker.Api/appsettings.json` connection string if needed.
3. Run:
   - `dotnet restore`
   - `dotnet build`
   - `dotnet run --project src/PersonalFinanceTracker.Api`
4. Verify:
   - API: `http://localhost:5000`
   - Health: `http://localhost:5000/health`

## Test
- `dotnet test`

## Notes
- Project targets `.NET 8` (`net8.0`).
- All API routes are under `/api`.
- Demo seed user is auto-created on startup:
  - `email: demo@finance.com`
  - `password: Demo@123`

- Additional demo user:
  - email: test@finance.com
  - password: Test@123
