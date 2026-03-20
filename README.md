# codexothon-finance-tracker-service

Backend API for Personal Finance Tracker.

## Stack
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- JWT auth + refresh tokens
- Background worker for recurring transactions

## Run locally
1. Configure `src/PersonalFinanceTracker.Api/appsettings.json` connection string.
2. Run:
   - `dotnet restore`
   - `dotnet run --project src/PersonalFinanceTracker.Api`
3. API runs on `http://localhost:5000`.

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
