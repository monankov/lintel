# Moving from Structr to Lintel

Lintel takes the parts of Structr that are still useful on .NET 10. For the other parts, .NET 10 or a well-known library
now does the job. This guide shows what to change in a project that uses Structr.

In most cases you only change the package reference and the `using` lines. Lintel also fixes many Structr bugs, so
some behavior is different; the important changes are listed for each package.

## 1. Packages

| Structr package | Lintel package | Namespace |
|---|---|---|
| `Structr.Operations`, `Structr.Notices` | `Lintel.Operations` | `Lintel.Operations`, `Lintel.Notices` |
| `Structr.Specifications`, `Structr.Collections`, sorting from `Structr.Abstractions` | `Lintel.Querying` | `Lintel.Specifications`, `Lintel.Collections`, `Lintel.Querying` |
| `Structr.Domain` | `Lintel.Domain` | `Lintel.Domain` |
| `Structr.Abstractions` (`TreeNode`, `HierarchyId`, sequential GUIDs, enum and string helpers), `Structr.IO.SequentialFileName` | `Lintel.Primitives` | `Lintel.Primitives` |
| `Structr.EntityFrameworkCore` | `Lintel.EntityFrameworkCore` | `Lintel.EntityFrameworkCore` |
| `Structr.AspNetCore.Validation` (attributes) | `Lintel.DataAnnotations` | `Lintel.DataAnnotations` |
| `Structr.AspNetCore` | `Lintel.AspNetCore` | `Lintel.AspNetCore.*` |
| `Structr.Configuration` | `Lintel.Settings` | `Lintel.Settings` |
| `Structr.Stateflows` | `Lintel.Stateflows` | `Lintel.Stateflows` |
| `Structr.Navigation` | `Lintel.Navigation` | `Lintel.Navigation` |

The other Structr packages are not moved. See [section 4](#4-what-is-not-moved) for replacements.

## 2. Steps

1. Move the project to .NET 10 (Lintel requires `net10.0`).
2. Replace the `Structr.*` package references with `Lintel.*` (see the table).
3. Replace the namespaces. A search and replace over the solution is usually enough:
   `Structr.Operations` → `Lintel.Operations`, `Structr.Notices` → `Lintel.Notices`, and so on.
4. In `_ViewImports.cshtml`: `@addTagHelper *, Structr.AspNetCore` → `@addTagHelper *, Lintel.AspNetCore`.
5. Build, fix the few API changes below, and run the tests.
6. Read the behavior changes of the packages you use. Most of them are bug fixes, but a test that expected the old
   behavior will fail.

## 3. Package by package

### Lintel.Operations

- `AddOperations`, `AddNotices`, the handler base classes (`OperationHandler`, `AsyncOperationHandler`,
  `NoticeHandler`, `AsyncNoticeHandler`) and all interfaces except `IOperationFilter` keep their signatures.
- **Breaking:** in `IOperationFilter.FilterAsync` the cancellation token is now the last parameter, as .NET
  recommends. Swap the last two parameters in every filter:

  ```csharp
  // Structr
  public Task<TResult> FilterAsync(TOperation operation, CancellationToken cancellationToken, OperationHandlerDelegate<TResult> next)
  // Lintel
  public Task<TResult> FilterAsync(TOperation operation, OperationHandlerDelegate<TResult> next, CancellationToken cancellationToken)
  ```

  Calls of `next()` do not change.
- New: `AddOperationFilter(typeof(MyFilter<,>))` checks the filter type and does not register it twice.
- New options: `HandlerServiceLifetime` and `NoticeServiceOptions.PublishStrategy`
  (`SequentialNoticePublishStrategy` by default, or `ParallelNoticePublishStrategy`).

Behavior: calling `AddNotices` twice with the same assembly no longer registers the handlers twice (in Structr every
handler then ran two times).

### Lintel.Querying

| Structr | Lintel |
|---|---|
| `SortOrder` from `Structr.Abstractions`; `OrderBy`/`ThenBy`/`PageBy` from `Structr.Abstractions.Extensions` | `using Lintel.Querying;` |
| `IPagedList<T>`, `PagedList<T>` from `Structr.Collections` | the same names in `Lintel.Collections` |
| `SerializablePagedList<T>` | `PagedResult<T>` (a record): `page.ToPagedResult()`, `result.ToPagedList()` |
| `ToPagedList(IQueryable, ...)` from `Structr.EntityFrameworkCore` | `ToPagedList` in `Lintel.Collections`; `ToPagedListAsync` in `Lintel.EntityFrameworkCore` |
| `Structr.Collections.Extensions.AutoMapper` (`MapTo`) | `page.Map(x => ...)` |

Behavior:
- `IEnumerable.ToPagedList(pageNumber, pageSize)` now really takes one page. In Structr the whole collection was the page.
- `PageBy(skip, take)` accepts `skip = 0` and `take = 1` (in Structr the first page did not work).
- Property names for sorting are not case-sensitive for `IEnumerable` too.
- An empty property name throws `ArgumentException`; an unknown one throws `InvalidOperationException`.

### Lintel.Domain

The classes and interfaces keep their names. `CreatedBy`, `ModifiedBy` and `DeletedBy` are `string?` now.

Behavior:
- The hash code of an entity does not change after it is saved, so it is not "lost" in a `HashSet`.
- An entity and its EF Core proxy are equal in both directions.
- `ValueObject` compares the properties of the value object type, and it no longer fails with indexers or with a
  reference to an object of the same type.

### Lintel.Primitives

| Structr | Lintel |
|---|---|
| `Structr.Abstractions` (`TreeNode`, `HierarchyId`), `Structr.Abstractions.Extensions` (`GetDisplayName`, `GetDescription`, `Cast`) | `Lintel.Primitives` |
| `Structr.Abstractions.Providers.SequentialGuid` | `Lintel.Primitives`; `AddSequentialGuidProvider()` is the same |
| `Structr.IO.SequentialFileName` | `Lintel.Primitives.SequentialFileName` |
| `SequentialFileName.NewFileNameWithMimeType(mime)` | not moved; use `NewFileNameWithExtension(extension)` |

Sequential GUIDs are the same as in Structr, byte by byte.

Behavior:
- `HierarchyId.Parse` is strict: empty, non-numeric and negative parts throw (Structr skipped them).
  `HierarchyId.Move` replaces only the start of the path, and `GetAncestor(n)` requires `1 <= n < GetLevel()`.
- `TreeNode.AddChild` moves a node from its old parent and rejects cycles.
- `GetDisplayName` and `GetDescription` return the name of the value when there is no attribute, instead of `null`.
- `Cast` also supports `Guid`, `DateOnly`, `TimeOnly`, `TimeSpan`, `DateTimeOffset`, `Uri` and other types with a
  `TypeConverter`.

### Lintel.EntityFrameworkCore

- `ApplyEntityConfiguration`, `ApplyValueObjectConfiguration`, `ApplyAuditableConfiguration`, `GetEntityTypes`,
  `DbContext.Audit(...)`, `DbContextOptions.GetService<T>()` and `ToPagedListAsync` keep their signatures.
  The options classes are in `Lintel.EntityFrameworkCore.Options`.
- Instead of calling `this.Audit(...)` in an overridden `SaveChanges`, you can add
  `options.AddInterceptors(new AuditSaveChangesInterceptor(TimeProvider.System, () => userName))`.
- `IncludeSoftDeleted()` shows soft-deleted rows and keeps the other query filters; `IgnoreQueryFilters()` turns off
  all filters.
- `UseLoggerProvider` is not moved: use `options.LogTo(...)` or `options.UseLoggerFactory(...)`.
- New: `ApplyValueObjectsAsComplexTypes()` (value objects in the columns of the entity) and `UseSequentialMigrationIds()`.

Behavior:
- A soft-deleted entity keeps its owned entities. In Structr EF Core deleted them, and their data was lost.
- Models with inheritance (TPH) work.
- The soft-delete filter is a named filter, so it works together with your own filters.
- Without a `TimeProvider`, the interceptor writes `DateTime.Now`, as Structr did. **On PostgreSQL use the constructor
  with `TimeProvider`**: Npgsql refuses local time in `timestamp with time zone` columns.

### Lintel.DataAnnotations

`using Structr.AspNetCore.Validation;` → `using Lintel.DataAnnotations;`. Attribute names, constructors, message
templates and client parameters are the same. The package does not depend on ASP.NET Core any more.
`AddAspNetCoreValidation()` (client-side adapters) is in `Lintel.AspNetCore`.

Behavior:
- An attribute no longer changes its own state during validation. In Structr the second validation could return the
  raw template `"{0} must be greater than {1}."`.
- The display name of the related property is used on the server too.
- Numbers of different types are compared by value (`int` and `long`, `decimal` and `double`).
- For `In` and `NotIn`, a string in the related property is one value, not a set of characters.
- Regular expressions have a 1-second timeout.

### Lintel.AspNetCore

- The namespaces `Structr.AspNetCore.Http`, `.Referrer`, `.Rewrite`, `.Routing`, `.TagHelpers`, `.Mvc` become
  `Lintel.AspNetCore.*` with the same names.
- `ClientAlertResult` and `ClientOptionControllerExtensions` were in `Microsoft.AspNetCore.Mvc`; now they are in
  `Lintel.AspNetCore.Client.Alerts` and `Lintel.AspNetCore.Client.Options`.
- `AddAspNetCore()` no longer registers `IActionContextAccessor` and `IUrlHelper` (`IActionContextAccessor` is
  obsolete in .NET 10). If you inject `IUrlHelper`, use `IUrlHelperFactory` or `LinkGenerator`.
- `PageSizeOptions` uses Bootstrap 5 by default. For Bootstrap 4 set `DropdownToggleAttribute = "data-toggle"`.
- Not moved: `JsonResponse`, `JsonSuccess`, `JsonError` (use `ProblemDetails` or `TypedResults`, or keep the class in
  your project), `JavaScriptResult`, `AjaxRedirectResult`, and the `DateOnly`/`TimeOnly` converters (built into .NET).

Behavior:
- **Security:** `RedirectToReferrer`, `GetReferrer` and `asp-referrer` accept only local URLs. In Structr the value
  was used as it was (an open redirect and a possible XSS).
- Client alerts are shown once. In Structr they appeared again on every following page.
- A new page size opens the first page.
- Tag helpers work in Razor Pages and other endpoints, not only in MVC.

### Lintel.Settings

`using Structr.Configuration;` → `using Lintel.Settings;`, `Structr.Configuration.Providers` →
`Lintel.Settings.Providers`. Signatures, file formats and the passphrase encryption are the same, so existing files work.

Behavior:
- JSON is read with System.Text.Json. Names are not case-sensitive, and comments and trailing commas are allowed, as
  before. **Enums are written by name** (`"Mode": "On"`), Structr wrote numbers; both are read.
- The file is written atomically, and reading and writing are thread-safe.
- New: `[Option(Protected = true)]` encrypts with ASP.NET Core Data Protection. Prefer it to `EncryptionPassphrase`
  for new settings.

### Lintel.Stateflows

`using Structr.Stateflows;` → `using Lintel.Stateflows;`. Configurations use the Stateless API as before.

Behavior:
- **All** configurations of an entity are applied, in the order of registration. In Structr only the last one was
  used, and the others were silently ignored.
- Classes based on `StateMachineConfiguration` are found by assembly scanning too.
- New: `IStateMachine.FireAsync` for async entry and exit actions. Your own `IStateMachine` classes need this method.

### Lintel.Navigation

`using Structr.Navigation;` → `using Lintel.Navigation;`, providers → `Lintel.Navigation.Providers`. JSON and XML
formats are the same.

Behavior:
- Menu and breadcrumbs in the same request both see the active item (in Structr the breadcrumbs could be empty).
- Titles are localized in the culture of each request (in Structr they stayed in the culture of the first request).
- `AddNavigation()` no longer calls `AddMemoryCache()`; the tree is kept in the navigation cache itself.
- JSON is read with System.Text.Json.

## 4. What is not moved

| Structr | Use instead |
|---|---|
| `Ensure` / `Check` | `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace`, `ArgumentOutOfRangeException.ThrowIf...` |
| `ITimestampProvider` | `TimeProvider` (`FakeTimeProvider` in tests) |
| `DateOnly` / `TimeOnly` JSON converters and type converters | built into .NET |
| `StringNumberJsonConverter` | `JsonNumberHandling.AllowReadingFromString` |
| `ToSnakeCase` / `ToHyphenCase` / `ToCamelCase` | `JsonNamingPolicy.SnakeCaseLower` / `KebabCaseLower` / `CamelCase` with `.ConvertName()` |
| `Contains` / `Replace` with `StringComparison` | the methods of `string` |
| `Shuffle` / `PickRandom` | `Random.Shared.Shuffle` / `Random.Shared.GetItems` |
| `ForEach`, `DateTimeExtensions` | `foreach`; `date?.ToString(format) ?? "-"` |
| `AsyncHelper.RunSync` | async code up to the top |
| `HashHelper`, `ObjectDumper` | `JsonSerializer` with `WriteIndented` |
| `Money<T>` | a `readonly record struct` in your domain |
| `Pbkdf2Hasher` | `PasswordHasher<TUser>` from ASP.NET Core Identity (check with real hashes in a test) |
| `Md5Hasher` | do not use MD5 for passwords; for checksums `MD5.HashData` and `Convert.ToHexString` |
| `StringEncryptor`, `EncryptingJsonConverter` | ASP.NET Core Data Protection, and a one-time tool to encrypt existing data again |
| Claims extensions | `ClaimsPrincipal.FindFirstValue` |
| `Structr.Configuration` for read-only settings | `IOptions<T>` / `IOptionsMonitor<T>` |
| `Structr.Validation` | FluentValidation, called from an `IOperationFilter` |
| `Structr.Email` | MailKit; Razor components with `HtmlRenderer` for templates; Mailpit or smtp4dev during development |
| `Structr.IO` (`MimeTypeHelper`, `FileHelper`, `PathHelper`) | `FileExtensionContentTypeProvider`, `File.*Async`, `IWebHostEnvironment` / `IFileProvider` |
| `Structr.SqlServer` | `Database.EnsureCreated` / `EnsureDeleted`; Testcontainers and Respawn in tests |
| `Structr.EntityFramework` (EF6) | move to EF Core; keep .NET Framework applications on Structr |
