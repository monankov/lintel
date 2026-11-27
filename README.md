<img src="assets/icon.png" alt="Lintel" width="64" height="64" align="right" />

# Lintel

[![CI](https://github.com/monankov/lintel/actions/workflows/ci.yml/badge.svg)](https://github.com/monankov/lintel/actions/workflows/ci.yml)

Lightweight building blocks for .NET 10 applications: an in-process mediator, specifications and paging,
domain base types, EF Core and ASP.NET Core integrations.

Lintel is the successor of [Structr](https://github.com/askalione/Structr): only the parts that the modern
.NET platform does not provide out of the box were carried over and rewritten for `net10.0`.

> Status: **pre-release** — the packages are covered by tests but not yet published to NuGet.

Each package has its own README with examples (the links in the table below). Moving a project from Structr:
[docs/migration-from-structr.md](docs/migration-from-structr.md).

## Packages

| Package | Purpose | Dependencies |
|---|---|---|
| [`Lintel.Operations`](src/Lintel.Operations/README.md) | Operations (commands/queries) with filter pipeline, notices (pub/sub) | — |
| [`Lintel.Querying`](src/Lintel.Querying/README.md) | Specifications, `PagedList<T>`, dynamic sorting | — |
| [`Lintel.Domain`](src/Lintel.Domain/README.md) | Entities, value objects, auditing and soft-delete contracts | — |
| [`Lintel.Primitives`](src/Lintel.Primitives/README.md) | `TreeNode`, `HierarchyId`, sequential GUIDs, enum display helpers | — |
| [`Lintel.EntityFrameworkCore`](src/Lintel.EntityFrameworkCore/README.md) | Auditing / soft-delete interceptors, conventions, paging, sequential migration ids | EF Core 10 (Relational), Domain, Querying |
| [`Lintel.DataAnnotations`](src/Lintel.DataAnnotations/README.md) | `RequiredIf`, `GreaterThan`, `In` and other conditional attributes | — |
| [`Lintel.AspNetCore`](src/Lintel.AspNetCore/README.md) | Paging, page size, sorting, form and badge tag helpers, rewrite rules, referrer, client alerts and options, route breadcrumbs and menus, localized validation messages, maintenance mode, security (proxy, allowed networks, failed attempts, headers) | ASP.NET Core, Querying, DataAnnotations, Navigation, Primitives |
| [`Lintel.Settings`](src/Lintel.Settings/README.md) | Typed settings in JSON/XML files, writable at runtime, hot reload, encrypted properties | Data Protection abstractions |
| [`Lintel.Stateflows`](src/Lintel.Stateflows/README.md) | Entity state machines on top of Stateless | Stateless |
| [`Lintel.Navigation`](src/Lintel.Navigation/README.md) | Menu and breadcrumbs from JSON | — |

The Dependencies column lists other packages. Packages with `Add...()` registration methods also depend on
`Microsoft.Extensions.DependencyInjection.Abstractions`.

## Build

Requires .NET SDK 10.0.

```
dotnet build Lintel.slnx
dotnet test --solution Lintel.slnx
dotnet pack Lintel.slnx -c Release -o artifacts
```

Package versions come from git tags (`v1.2.3`) via [MinVer](https://github.com/adamralph/minver);
dependency versions are managed centrally in `Directory.Packages.props`.

### PostgreSQL integration tests

`tests/Lintel.EntityFrameworkCore.PostgreSql.Tests` runs against PostgreSQL 18 in Docker
([Testcontainers](https://dotnet.testcontainers.org/)). Without Docker these tests are skipped;
CI sets `LINTEL_REQUIRE_DOCKER=true` so that they fail instead.

Testcontainers needs Docker API 1.44 or later (Docker Desktop 4.27 or later).

### Code coverage

```
dotnet tool restore
dotnet test --solution Lintel.slnx -p:CollectCoverage=true
dotnet reportgenerator "-reports:TestResults/*.cobertura.xml" "-targetdir:TestResults/coverage" "-reporttypes:Html;TextSummary"
```

The report is `TestResults/coverage/index.html`. Only `Lintel.*` assemblies are measured (`tests/coverage.config`).
CI fails when line coverage drops below 95% or branch coverage below 85%.

## License

MIT. Portions derived from Structr (MIT).
