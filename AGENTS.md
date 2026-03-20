# AGENTS.md

## Purpose
This repository contains the backend API for the Personal Finance Tracker.  
The primary goal is to keep the API stable, testable, and ready for QA automation.

## Engineering Guidelines
- Preserve API compatibility for existing frontend flows unless a breaking change is explicitly approved.
- Keep all endpoints under `/api`.
- Validate all financial inputs server-side.
- Scope all user data queries by authenticated `userId`.
- Prefer deterministic seed data for local/QA runs.

## QA Readiness Rules
- Do not remove demo seed users:
  - `demo@finance.com / Demo@123`
  - `test@finance.com / Test@123`
- Keep startup migration + seeding enabled for local QA environment.
- Ensure `swagger` is available in Development mode.
- Keep `/health` endpoint returning successful status when app is up.

## Local Run
- `dotnet restore`
- `dotnet build`
- `dotnet run --project src/PersonalFinanceTracker.Api`

