<!--
Sync Impact Report (temporary; remove before commit)
Version change: (unversioned template) -> 1.0.0
Modified principles: none renamed (all placeholders replaced; initial adoption)
Added sections: Core Principles I-V, Technology & Platform Constraints,
  Development Workflow & Quality Gates, Governance
Removed sections: none
Follow-up TODOs: none
-->
# ContosoDashboard Constitution

## Core Principles

### I. Training-Only Scope, Offline-First
ContosoDashboard is a training application for Spec-Driven Development and MUST NOT be
positioned or built as a production system. All features MUST run locally and offline: no
runtime dependency on cloud services, external APIs, or paid subscriptions. Mock
authentication remains the authentication mechanism; any production-grade identity concern
is documented as a known limitation rather than implemented.
Rationale: Training must be available to every student without accounts or connectivity.

### II. Infrastructure Abstraction via Interfaces
Every infrastructure dependency (file storage, database access beyond EF Core, identity,
external integrations) MUST be accessed through an interface registered with dependency
injection, so a local implementation can be swapped for a cloud one (for example
`IFileStorageService`: local filesystem now, Azure Blob Storage later) with no business logic
changes. Business logic MUST NOT reference concrete infrastructure types. File uploads MUST
generate a unique (GUID-based) storage path before the database insert to avoid duplicate-key
violations and orphaned records.
Rationale: Teaches proper abstraction and keeps the cloud migration path configuration-only.

### III. Security by Default (NON-NEGOTIABLE)
Every protected page MUST carry `[Authorize]` (with role requirements where applicable), and
every service method that reads or mutates user-scoped data MUST enforce authorization itself
(defense in depth: middleware, page attributes, service checks). Services MUST prevent IDOR:
a user MUST only access data they are authorized for under the role hierarchy
(Administrator, Project Manager, Team Lead, Employee). Security headers and cookie settings
MUST NOT be weakened. User input MUST be validated at system boundaries, and no secrets may
be committed to the repository.
Rationale: Security behavior is a core teaching objective and cannot rely on UI-only checks.

### IV. Layered Architecture
Code MUST stay separated into Models (entities), Data (`ApplicationDbContext`, EF Core
configuration and seed data), Services (business logic and authorization), and
Pages/Shared (Blazor UI). Pages MUST call services and MUST NOT access `DbContext` directly
or contain business rules. Schema changes MUST be expressed through the EF Core model and
accompanied by seed data updates when they affect sample users, projects, or tasks.
Rationale: Clear boundaries make the codebase easy to learn and to specify against.

### V. Simplicity and Educational Clarity
Solutions MUST favor the simplest approach that meets the specified requirement (YAGNI).
New dependencies, patterns, or layers MUST be justified in the plan. Code MUST remain
readable to learners: descriptive names, small focused methods, and comments only where
intent is not obvious. Known limitations MUST be documented, not hidden.
Rationale: The codebase is a teaching artifact; clarity outranks cleverness.

## Technology & Platform Constraints

- Framework: ASP.NET Core 8.0 with Blazor Server (.NET 8 SDK).
- Data: SQL Server LocalDB via Entity Framework Core; no other database engines.
- Authentication: cookie-based mock authentication with claims-based identity and
  role-based access control.
- UI: Bootstrap 5.3 with Bootstrap Icons; no additional UI frameworks without plan approval.
- Packages MUST be compatible with offline restore after initial setup and MUST NOT require
  cloud service credentials.

## Development Workflow & Quality Gates

- Work follows Spec-Driven Development: specification, plan, tasks, then implementation,
  using the artifacts under `.specify/` and `StakeholderDocs/` as input.
- Every plan MUST include a Constitution Check against Principles I-V before design and
  after design.
- Every change MUST build cleanly (`dotnet build`) with no new warnings introduced.
- Any change touching authentication, authorization, or user-scoped data MUST be reviewed
  explicitly against Principle III, including a manual check with at least two different
  mock users to confirm isolation.
- Features that add persisted data MUST update seed data so a fresh LocalDB database runs
  the application end-to-end.

## Governance

This constitution supersedes other practices for this repository. Amendments MUST be made by
editing `.specify/memory/constitution.md` with a documented rationale, an updated version
line, and a review of dependent specs, plans, and tasks for conflicts. Versioning follows
semantic versioning: MAJOR for backward-incompatible principle removals or redefinitions,
MINOR for added or materially expanded principles or sections, PATCH for clarifications and
wording fixes. All specs, plans, and code reviews MUST verify compliance with this
constitution, and any deviation MUST be justified in writing in the plan's complexity
tracking. Use `README.md` for runtime development guidance.

**Version**: 1.0.0 | **Ratified**: 2026-11-01 | **Last Amended**: 2026-11-01
