# Turso Client Design

**Date:** 2026-03-04
**Status:** Approved

## Problem

API and Worker are separate containers, each with their own local `mymemo.db` SQLite file. When the Worker tries to insert a transcription referencing a `chunk_id` created by the API, the FK constraint fails — the Worker's DB is empty.

`Turso__Url` and `Turso__AuthToken` are already configured in the Container Apps, but the code uses `Microsoft.Data.Sqlite` which only opens local files.

## Approach

Implement a `TursoDbConnection : DbConnection` ADO.NET wrapper that communicates with Turso's HTTP pipeline API. All repositories stay unchanged. Factory selection is config-driven: Turso in production, SQLite in local dev.

## Components

### New files — `MyMemo.Shared/Database/Turso/`

| File | Responsibility |
|------|---------------|
| `TursoConnectionFactory.cs` | Implements `IDbConnectionFactory`, creates `TursoDbConnection` |
| `TursoDbConnection.cs` | Extends `DbConnection`, holds `HttpClient` + base URL + auth token |
| `TursoDbCommand.cs` | Extends `DbCommand`, translates Dapper calls to Turso HTTP requests |
| `TursoDataReader.cs` | Extends `DbDataReader`, serves JSON response rows to Dapper's mapper |
| `TursoDbParameter.cs` | Extends `DbParameter` |
| `TursoDbParameterCollection.cs` | Extends `DbParameterCollection` |

### Modified files

| File | Change |
|------|--------|
| `IDbConnectionFactory.cs` | Return type `SqliteConnection` → `System.Data.Common.DbConnection` |
| `SqliteConnectionFactory.cs` | Cast return to `DbConnection` |
| `DatabaseInitializer.cs` | Wrap `PRAGMA` statements in try-catch |
| `MyMemo.Api/Program.cs` | Factory selected based on `Turso:Url` config |
| `MyMemo.Worker/Program.cs` | Factory selected based on `Turso:Url` config |

## Turso HTTP API

**Endpoint:** `POST https://{db}.turso.io/v2/pipeline`
**Auth:** `Authorization: Bearer {token}`

**Request:**
```json
{
  "requests": [
    {
      "type": "execute",
      "stmt": {
        "sql": "SELECT id FROM sessions WHERE user_id = @userId",
        "named_args": [
          { "name": "userId", "value": { "type": "text", "value": "user_abc" } }
        ]
      }
    },
    { "type": "close" }
  ]
}
```

**Response:**
```json
{
  "results": [
    {
      "type": "ok",
      "response": {
        "type": "execute",
        "result": {
          "cols": [{ "name": "id", "decltype": "TEXT" }],
          "rows": [[{ "type": "text", "value": "abc123" }]],
          "affected_row_count": 0,
          "last_insert_rowid": null
        }
      }
    },
    { "type": "ok", "response": { "type": "close" } }
  ]
}
```

## Parameter Handling

Dapper emits `@paramName` syntax. `TursoDbCommand` extracts parameter names from the collection and maps them to Turso `named_args`:

| .NET type | Turso type |
|-----------|-----------|
| `string` | `"text"` |
| `int`, `long` | `"integer"` |
| `double`, `float` | `"real"` |
| `bool` | `"integer"` (0/1) |
| `null`, `DBNull` | `"null"` |

## Config-Based Factory Selection

```csharp
IDbConnectionFactory dbFactory = builder.Configuration["Turso:Url"] is { } tursoUrl
    ? new TursoConnectionFactory(tursoUrl, builder.Configuration["Turso:AuthToken"]!)
    : new SqliteConnectionFactory(dbConnectionString);
```

`Turso:Url` can be `libsql://` or `https://` — both are normalised to `https://` internally.

## Local Dev

No change. SQLite is used when `Turso:Url` is absent.

## What Does Not Change

- All 5 repositories
- All Dapper queries and SQL
- `DatabaseInitializer` schema + migrations (just PRAGMA calls wrapped in try-catch)
- No new NuGet packages required (`System.Net.Http.Json` is already in the SDK)
