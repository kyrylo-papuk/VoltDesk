# VoltDesk

Internal desktop system for an electrical design studio that works on US building projects
(hospitals, data centers, factories, offices): client CRM, project tracking by design stage,
a task board and electrical panel schedules with load calculations.

> Work in progress. Built in phases; see the roadmap below.

## Stack

- **Desktop:** WPF on .NET 10, MVVM (CommunityToolkit.Mvvm), Generic Host DI
- **API:** ASP.NET Core Web API, EF Core, health checks
- **Database:** SQL Server 2025 (Docker)
- **CI:** GitHub Actions

## Solution layout

| Project | Purpose |
|---|---|
| `src/VoltDesk.Core` | Domain model and business rules, no infrastructure dependencies |
| `src/VoltDesk.Infrastructure` | EF Core persistence (SQL Server) |
| `src/VoltDesk.Api` | REST API used by the desktop client |
| `src/VoltDesk.Desktop` | WPF client |

## Run locally

Requires the .NET 10 SDK and Docker.

```bash
docker compose up -d db                  # SQL Server on localhost:1433
dotnet run --project src/VoltDesk.Api    # API on http://localhost:5080, creates the database
dotnet run --project src/VoltDesk.Desktop
```

`GET http://localhost:5080/health` reports whether the API can reach the database.

## Roadmap

- [x] 0. Solution skeleton, SQL Server in Docker, CI
- [ ] 1. Domain and EF Core: clients, projects, design stages, tasks
- [ ] 2. REST API: CRUD, validation, JWT roles, audit log, integration tests
- [ ] 3. Desktop: sign-in, project list and cards
- [ ] 4. Desktop: task board
- [ ] 5. Panel schedules: load calculation, phase balancing, Excel export
- [ ] 6. Google Drive, Calendar and Sheets automation
- [ ] 7. Packaging: Docker Compose, installer in Releases, demo
