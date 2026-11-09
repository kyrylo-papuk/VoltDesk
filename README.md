# VoltDesk

Internal desktop system for an electrical design studio that works on US building projects
(hospitals, data centers, factories, offices): client CRM, project tracking by design stage,
a task board and electrical panel schedules with load calculations.

> Work in progress. Built in phases; see the roadmap below.

## Stack

- **Desktop:** WPF on .NET 10, MVVM (CommunityToolkit.Mvvm), Generic Host DI
- **API:** ASP.NET Core Web API, EF Core, health checks
- **Database:** SQL Server 2025 (Docker)
- **Tests:** xUnit v3 on Microsoft.Testing.Platform
- **CI:** GitHub Actions

## Domain

| Entity | What it holds |
|---|---|
| Client | Customer company and its contacts, one of them primary |
| Employee | Studio staff: engineers, project managers, admins |
| Project | Design contract for one building: number, client, location, fee, manager, current design stage |
| Milestone | Due date and completion of each design stage: SD → DD → CD → Permit → CA |
| Task | Work item in a stage: discipline (power, lighting, fire alarm, low voltage), assignee, status, estimate |

Business rules live in `VoltDesk.Core` and are covered by unit tests:

- A project moves through the design stages in order. A stage cannot be closed while it has open tasks.
  Closing Construction Administration completes the project.
- Stage due dates must follow the stage order. Completed stages cannot be rescheduled.
- Only active project managers and admins can manage a project.
- Projects on hold or closed cannot get new tasks or advance.
- A task cannot leave To Do without an assignee. Done records the completion date.
- A contact needs an email or a phone number. Each client has exactly one primary contact and
  no duplicate contact emails (also enforced by a filtered unique index).
- Locations use real US state codes. Enums are stored as readable strings in the database.

## Solution layout

| Project | Purpose |
|---|---|
| `src/VoltDesk.Core` | Domain model and business rules, no infrastructure dependencies |
| `src/VoltDesk.Infrastructure` | EF Core persistence (SQL Server), migrations, demo data |
| `src/VoltDesk.Api` | REST API used by the desktop client |
| `src/VoltDesk.Desktop` | WPF client |
| `tests/VoltDesk.Core.Tests` | Unit tests for the domain rules |

## Run locally

Requires the .NET 10 SDK and Docker.

```bash
docker compose up -d db                  # SQL Server on 127.0.0.1:1433
dotnet run --project src/VoltDesk.Api    # API on http://localhost:5080
dotnet run --project src/VoltDesk.Desktop
```

In Development the API applies migrations on start and fills an empty database with a fictional
demo studio: 8 employees, 5 clients, 6 projects in different stages, 30 tasks.
`GET http://localhost:5080/health` reports whether the API can reach the database.

```bash
dotnet test                              # unit tests
dotnet tool restore                      # dotnet-ef, pinned in dotnet-tools.json
dotnet ef migrations add <Name> --project src/VoltDesk.Infrastructure \
  --startup-project src/VoltDesk.Api --output-dir Persistence/Migrations
```

## Roadmap

- [x] 0. Solution skeleton, SQL Server in Docker, CI
- [x] 1. Domain and EF Core: clients, projects, design stages, tasks
- [ ] 2. REST API: CRUD, validation, JWT roles, audit log, integration tests
- [ ] 3. Desktop: sign-in, project list and cards
- [ ] 4. Desktop: task board
- [ ] 5. Panel schedules: load calculation, phase balancing, Excel export
- [ ] 6. Google Drive, Calendar and Sheets automation
- [ ] 7. Packaging: Docker Compose, installer in Releases, demo
