# VoltDesk

Internal desktop system for an electrical design studio that works on US building projects
(hospitals, data centers, factories, offices): client CRM, project tracking by design stage,
a task board and electrical panel schedules with load calculations.

> Work in progress. Built in phases; see the roadmap below.

## Stack

- **Desktop:** WPF on .NET 10, MVVM (CommunityToolkit.Mvvm), Generic Host DI, typed HttpClient, custom flat theme
- **API:** ASP.NET Core Web API, EF Core, JWT bearer auth with roles, ProblemDetails errors, OpenAPI + Swagger UI
- **Database:** SQL Server 2025 (Docker)
- **Tests:** xUnit v3 on Microsoft.Testing.Platform, integration tests on a real SQL Server via Testcontainers
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

## API

REST API under `/api`, documented with OpenAPI (Swagger UI at `/swagger` in Development).

| Area | Endpoints |
|---|---|
| Auth | `POST /auth/login`, `GET /auth/me` |
| Clients | list with search, details, create, rename, add / edit / remove contacts, history |
| Projects | list with filters, details with milestones and tasks, create, edit, change manager, schedule milestones, advance / hold / resume / cancel, history |
| Tasks | list, my open tasks, create, edit, assign, move on the board |
| Employees | list, create, change role, deactivate / reactivate |

**Roles.** Engineers read everything and work on tasks. Project managers also manage clients,
projects and tasks. Admins also manage employees. Every token is checked against the database
on each request, so deactivating an employee or changing their role takes effect immediately.

**Errors** are RFC 7807 problem responses the desktop client can show as is:

| Status | When |
|---|---|
| 400 | Request validation failed, or a malformed version token |
| 401 / 403 | Not signed in / role not allowed |
| 409 | Someone else changed the record (stale version), or a duplicate number, name or email |
| 422 | A business rule was violated, e.g. closing a stage with open tasks |
| 429 | Too many sign-in attempts |

### Design decisions

- **Optimistic concurrency.** Clients, projects, tasks and employees have a SQL Server `rowversion`.
  Every DTO carries it as `Version` and every change sends it back. Editing a contact or a
  milestone also touches the parent client or project row, so a stale edit of a child is caught
  at aggregate level.
- **Audit log.** Every save records who changed which property from what to what, in the same
  transaction as the data. History is grouped by aggregate: a project's history includes its
  tasks and milestones. Password hashes are masked.
- **No repository layer.** `DbContext` already is a unit of work; controllers load an aggregate,
  call a domain method and save. Reads are `AsNoTracking` projections straight into DTOs.
- **Rowversion and timestamps are shadow properties**, so the domain model stays free of
  persistence concerns.
- **No retrying execution strategy.** Each save opens a transaction for data plus audit entries,
  and a retrying strategy cannot safely replay that. Startup waits for SQL Server instead.

## Desktop client

| Screen | What it does |
|---|---|
| Sign-in | Email and password, demo account shortcuts, live server status |
| Projects | Search and status filter, stage and status badges, overdue stage dates in red |
| Project card | Stage timeline with editable due dates, close stage / hold / resume / cancel, tasks by stage, change history |
| Task board | Kanban of tasks in active projects: drag cards or use arrows, filter by project and assignee, edit and assign in a side panel, new task dialog |
| Clients | Client list with search, contacts with primary flag, add and remove contacts |
| New project | Dialog with client, building type, US state and manager pickers |

- MVVM throughout: pages are view models, views are picked by `DataTemplate`, navigation keeps a back stack.
- Actions are shown by role: engineers see projects and clients read-only and work on the task board.
- Board moves are optimistic: the card moves at once and goes back with the server's reason if the move is refused
  (for example, a task without an assignee cannot start).
- API errors arrive as the server's own messages. A version conflict reloads the record and says why;
  an expired session returns to sign-in.
- If WPF renders a blank window (some VMs and remote sessions), start it with
  `VOLTDESK_SOFTWARE_RENDERING=1` to switch to software rendering.

## Solution layout

| Project | Purpose |
|---|---|
| `src/VoltDesk.Core` | Domain model and business rules, no infrastructure dependencies |
| `src/VoltDesk.Infrastructure` | EF Core persistence (SQL Server), migrations, demo data |
| `src/VoltDesk.Contracts` | Request and response DTOs shared by the API and the desktop client |
| `src/VoltDesk.Api` | REST API used by the desktop client |
| `src/VoltDesk.Desktop` | WPF client |
| `tests/VoltDesk.Core.Tests` | Unit tests for the domain rules |
| `tests/VoltDesk.Api.Tests` | Integration tests: real API + SQL Server in a container |

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

Demo accounts, all with the password `VoltDesk-demo-2026`:

| Email | Role |
|---|---|
| `avery.stone@voltdesk.example` | Admin |
| `dana.whitfield@voltdesk.example` | Project manager |
| `sam.ortega@voltdesk.example` | Engineer |

Outside Development, set `ConnectionStrings__VoltDesk` and `Jwt__SigningKey` (32+ characters).

```bash
dotnet test                              # unit + integration tests (Docker required)
dotnet tool restore                      # dotnet-ef, pinned in dotnet-tools.json
dotnet ef migrations add <Name> --project src/VoltDesk.Infrastructure \
  --startup-project src/VoltDesk.Api --output-dir Persistence/Migrations
```

## Roadmap

- [x] 0. Solution skeleton, SQL Server in Docker, CI
- [x] 1. Domain and EF Core: clients, projects, design stages, tasks
- [x] 2. REST API: CRUD, validation, JWT roles, audit log, integration tests
- [x] 3. Desktop: sign-in, project list and cards, clients
- [x] 4. Desktop: task board
- [ ] 5. Panel schedules: load calculation, phase balancing, Excel export
- [ ] 6. Google Drive, Calendar and Sheets automation
- [ ] 7. Packaging: Docker Compose, installer in Releases, demo
