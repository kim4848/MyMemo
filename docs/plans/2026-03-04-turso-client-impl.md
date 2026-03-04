# Turso Client Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the local `Microsoft.Data.Sqlite` connection with a Turso HTTP API wrapper so API and Worker share the same database.

**Architecture:** Implement `TursoDbConnection : DbConnection` + `TursoDbCommand : DbCommand` + `TursoDataReader : DbDataReader` that talk to Turso's `/v2/pipeline` HTTP endpoint. Factory selection is config-driven: use `TursoConnectionFactory` when `Turso:Url` is set, fall back to `SqliteConnectionFactory` for local dev. All repositories and Dapper queries remain unchanged.

**Tech Stack:** .NET 8, Dapper, `System.Net.Http.Json` (in SDK), xUnit, FluentAssertions, NSubstitute. No new NuGet packages required.

---

## Background: Turso HTTP API

Every SQL statement goes through `POST https://{db}.turso.io/v2/pipeline` with `Authorization: Bearer {token}`.

Request shape:
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

Response shape:
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
          "affected_row_count": 1,
          "last_insert_rowid": "42"
        }
      }
    },
    { "type": "ok", "response": { "type": "close" } }
  ]
}
```

`named_args` name matches what's in SQL: `@userId` in SQL → `"name": "userId"` in args (no prefix).

.NET → Turso type mapping:

| .NET | Turso `type` |
|------|-------------|
| `string` | `"text"` |
| `int`, `long` | `"integer"` |
| `double`, `float`, `decimal` | `"real"` |
| `bool` | `"integer"` (0/1) |
| `null`, `DBNull` | `"null"` |
| anything else | `"text"` (`.ToString()`) |

---

## Task 1: Widen `IDbConnectionFactory` return type

**Files:**
- Modify: `backend/src/MyMemo.Shared/Database/IDbConnectionFactory.cs`
- Modify: `backend/src/MyMemo.Shared/Database/SqliteConnectionFactory.cs`
- Modify: `backend/tests/MyMemo.Shared.Tests/Repositories/TestDbFixture.cs`

**Step 1: Update the interface**

Replace the file contents:

```csharp
using System.Data.Common;

namespace MyMemo.Shared.Database;

public interface IDbConnectionFactory
{
    Task<DbConnection> CreateConnectionAsync();
}
```

**Step 2: Update `SqliteConnectionFactory`**

```csharp
using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace MyMemo.Shared.Database;

public sealed class SqliteConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public async Task<DbConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
```

**Step 3: Update `TestDbFixture`** — change `SqliteConnection _keepAlive` field usage (the keeper still uses `SqliteConnection` directly, but `Factory` returns `DbConnection`):

```csharp
using System.Data.Common;
using Microsoft.Data.Sqlite;
using MyMemo.Shared.Database;

namespace MyMemo.Shared.Tests.Repositories;

public sealed class TestDbFixture : IDisposable
{
    private static int _counter;
    private readonly SqliteConnection _keepAlive;

    public IDbConnectionFactory Factory { get; }

    public TestDbFixture()
    {
        var dbName = $"testdb_{Interlocked.Increment(ref _counter)}";
        var connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

        _keepAlive = new SqliteConnection(connectionString);
        _keepAlive.Open();

        Factory = new SqliteConnectionFactory(connectionString);
        DatabaseInitializer.Initialize(Factory).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _keepAlive.Dispose();
    }
}
```

**Step 4: Run existing tests to confirm no regressions**

```bash
cd backend && dotnet test tests/MyMemo.Shared.Tests --verbosity normal
```

Expected: all green.

**Step 5: Commit**

```bash
git add backend/src/MyMemo.Shared/Database/IDbConnectionFactory.cs \
        backend/src/MyMemo.Shared/Database/SqliteConnectionFactory.cs \
        backend/tests/MyMemo.Shared.Tests/Repositories/TestDbFixture.cs
git commit -m "refactor(db): widen IDbConnectionFactory return type to DbConnection"
```

---

## Task 2: `TursoDbParameter` and `TursoDbParameterCollection`

**Files:**
- Create: `backend/src/MyMemo.Shared/Database/Turso/TursoDbParameter.cs`
- Create: `backend/src/MyMemo.Shared/Database/Turso/TursoDbParameterCollection.cs`

These are minimal ADO.NET stubs. Dapper uses them to store `@paramName → value` pairs.

**Step 1: Create `TursoDbParameter.cs`**

```csharp
using System.Data;
using System.Data.Common;

namespace MyMemo.Shared.Database.Turso;

public sealed class TursoDbParameter : DbParameter
{
    public override DbType DbType { get; set; } = DbType.String;
    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
    public override bool IsNullable { get; set; }
    public override string ParameterName { get; set; } = "";
    public override int Size { get; set; }
    public override string SourceColumn { get; set; } = "";
    public override bool SourceColumnNullMapping { get; set; }
    public override object? Value { get; set; }
    public override void ResetDbType() { DbType = DbType.String; }
}
```

**Step 2: Create `TursoDbParameterCollection.cs`**

```csharp
using System.Collections;
using System.Data.Common;

namespace MyMemo.Shared.Database.Turso;

public sealed class TursoDbParameterCollection : DbParameterCollection
{
    private readonly List<TursoDbParameter> _params = [];

    public override int Count => _params.Count;
    public override object SyncRoot => ((ICollection)_params).SyncRoot;

    public override int Add(object value)
    {
        _params.Add((TursoDbParameter)value);
        return _params.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var v in values) Add(v);
    }

    public override void Clear() => _params.Clear();

    public override bool Contains(object value) => _params.Contains((TursoDbParameter)value);
    public override bool Contains(string value) => _params.Any(p => p.ParameterName == value);

    public override void CopyTo(Array array, int index) => ((ICollection)_params).CopyTo(array, index);

    public override IEnumerator GetEnumerator() => _params.GetEnumerator();

    public override int IndexOf(object value) => _params.IndexOf((TursoDbParameter)value);
    public override int IndexOf(string parameterName) => _params.FindIndex(p => p.ParameterName == parameterName);

    public override void Insert(int index, object value) => _params.Insert(index, (TursoDbParameter)value);

    public override void Remove(object value) => _params.Remove((TursoDbParameter)value);
    public override void RemoveAt(int index) => _params.RemoveAt(index);
    public override void RemoveAt(string parameterName) => _params.RemoveAll(p => p.ParameterName == parameterName);

    protected override DbParameter GetParameter(int index) => _params[index];
    protected override DbParameter GetParameter(string parameterName) =>
        _params.First(p => p.ParameterName == parameterName);

    protected override void SetParameter(int index, DbParameter value) => _params[index] = (TursoDbParameter)value;
    protected override void SetParameter(string parameterName, DbParameter value) =>
        _params[_params.FindIndex(p => p.ParameterName == parameterName)] = (TursoDbParameter)value;

    public IReadOnlyList<TursoDbParameter> All => _params;
}
```

**Step 3: Build to confirm no errors**

```bash
cd backend && dotnet build src/MyMemo.Shared --no-restore
```

Expected: Build succeeded, 0 errors.

**Step 4: Commit**

```bash
git add backend/src/MyMemo.Shared/Database/Turso/
git commit -m "feat(turso): add TursoDbParameter and TursoDbParameterCollection"
```

---

## Task 3: `TursoDataReader` with unit tests

**Files:**
- Create: `backend/src/MyMemo.Shared/Database/Turso/TursoDataReader.cs`
- Create: `backend/tests/MyMemo.Shared.Tests/Database/TursoDataReaderTests.cs`

`TursoDataReader` wraps the parsed Turso result and serves rows to Dapper.

**Step 1: Write the failing tests first**

Create `backend/tests/MyMemo.Shared.Tests/Database/TursoDataReaderTests.cs`:

```csharp
using FluentAssertions;
using MyMemo.Shared.Database.Turso;

namespace MyMemo.Shared.Tests.Database;

public class TursoDataReaderTests
{
    private static TursoDataReader MakeReader(
        string[] colNames,
        object?[][] rows)
    {
        var cols = colNames.Select(n => new TursoCol(n, "TEXT")).ToList();
        var tursoRows = rows.Select(row =>
            row.Select(v => v is null
                ? new TursoValue("null", null)
                : new TursoValue("text", v.ToString()!)).ToArray()
        ).ToList();
        return new TursoDataReader(cols, tursoRows);
    }

    [Fact]
    public void FieldCount_ReturnsColumnCount()
    {
        var reader = MakeReader(["id", "title"], []);
        reader.FieldCount.Should().Be(2);
    }

    [Fact]
    public void GetName_ReturnsColumnName()
    {
        var reader = MakeReader(["id", "title"], []);
        reader.GetName(0).Should().Be("id");
        reader.GetName(1).Should().Be("title");
    }

    [Fact]
    public void GetOrdinal_ReturnsColumnIndex()
    {
        var reader = MakeReader(["id", "title"], []);
        reader.GetOrdinal("title").Should().Be(1);
    }

    [Fact]
    public void Read_ReturnsTrueWhileRowsAvailable()
    {
        var reader = MakeReader(["id"], [["abc"], ["def"]]);
        reader.Read().Should().BeTrue();
        reader.Read().Should().BeTrue();
        reader.Read().Should().BeFalse();
    }

    [Fact]
    public void GetValue_ReturnsCorrectValue()
    {
        var reader = MakeReader(["id", "title"], [["abc123", "My Session"]]);
        reader.Read();
        reader.GetValue(0).Should().Be("abc123");
        reader.GetValue(1).Should().Be("My Session");
    }

    [Fact]
    public void IsDBNull_ReturnsTrueForNullValue()
    {
        var reader = MakeReader(["id", "title"], [["abc", null]]);
        reader.Read();
        reader.IsDBNull(1).Should().BeTrue();
    }

    [Fact]
    public void GetValue_ReturnsDBNull_ForNullTursoValue()
    {
        var reader = MakeReader(["title"], [[null]]);
        reader.Read();
        reader.GetValue(0).Should().Be(DBNull.Value);
    }
}
```

**Step 2: Run to confirm it fails**

```bash
cd backend && dotnet test tests/MyMemo.Shared.Tests --filter "TursoDataReaderTests" --verbosity normal
```

Expected: FAIL — `TursoDataReader`, `TursoCol`, `TursoValue` do not exist yet.

**Step 3: Implement `TursoDataReader.cs`**

```csharp
using System.Data.Common;

namespace MyMemo.Shared.Database.Turso;

public record TursoCol(string Name, string DeclType);
public record TursoValue(string Type, string? Value);

public sealed class TursoDataReader(List<TursoCol> cols, List<TursoValue[]> rows) : DbDataReader
{
    private int _rowIndex = -1;

    public override int FieldCount => cols.Count;
    public override bool HasRows => rows.Count > 0;
    public override bool IsClosed => false;
    public override int RecordsAffected => -1;
    public override int Depth => 0;

    public override bool Read()
    {
        _rowIndex++;
        return _rowIndex < rows.Count;
    }

    public override string GetName(int ordinal) => cols[ordinal].Name;

    public override int GetOrdinal(string name)
    {
        var idx = cols.FindIndex(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) throw new IndexOutOfRangeException($"Column '{name}' not found.");
        return idx;
    }

    public override bool IsDBNull(int ordinal)
    {
        var v = rows[_rowIndex][ordinal];
        return v.Type == "null" || v.Value is null;
    }

    public override object GetValue(int ordinal)
    {
        var v = rows[_rowIndex][ordinal];
        if (v.Type == "null" || v.Value is null) return DBNull.Value;
        return v.Type switch
        {
            "integer" => long.TryParse(v.Value, out var l) ? (object)l : v.Value,
            "real"    => double.TryParse(v.Value, System.Globalization.NumberStyles.Any,
                             System.Globalization.CultureInfo.InvariantCulture, out var d) ? (object)d : v.Value,
            _         => v.Value
        };
    }

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++) values[i] = GetValue(i);
        return count;
    }

    public override string GetString(int ordinal)   => rows[_rowIndex][ordinal].Value ?? "";
    public override long GetInt64(int ordinal)       => long.Parse(rows[_rowIndex][ordinal].Value!);
    public override int GetInt32(int ordinal)        => int.Parse(rows[_rowIndex][ordinal].Value!);
    public override double GetDouble(int ordinal)    => double.Parse(rows[_rowIndex][ordinal].Value!,
                                                            System.Globalization.CultureInfo.InvariantCulture);
    public override bool GetBoolean(int ordinal)     => rows[_rowIndex][ordinal].Value != "0";
    public override byte GetByte(int ordinal)        => byte.Parse(rows[_rowIndex][ordinal].Value!);
    public override char GetChar(int ordinal)        => rows[_rowIndex][ordinal].Value![0];
    public override Guid GetGuid(int ordinal)        => Guid.Parse(rows[_rowIndex][ordinal].Value!);
    public override short GetInt16(int ordinal)      => short.Parse(rows[_rowIndex][ordinal].Value!);
    public override float GetFloat(int ordinal)      => float.Parse(rows[_rowIndex][ordinal].Value!,
                                                            System.Globalization.CultureInfo.InvariantCulture);
    public override decimal GetDecimal(int ordinal)  => decimal.Parse(rows[_rowIndex][ordinal].Value!,
                                                            System.Globalization.CultureInfo.InvariantCulture);
    public override DateTime GetDateTime(int ordinal)=> DateTime.Parse(rows[_rowIndex][ordinal].Value!);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;

    public override string GetDataTypeName(int ordinal) => cols[ordinal].DeclType;
    public override Type GetFieldType(int ordinal) => typeof(string);

    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool NextResult() => false;
    public override System.Collections.IEnumerator GetEnumerator() =>
        new System.Data.Common.DbEnumerator(this);
}
```

**Step 4: Run tests to confirm they pass**

```bash
cd backend && dotnet test tests/MyMemo.Shared.Tests --filter "TursoDataReaderTests" --verbosity normal
```

Expected: all 7 pass.

**Step 5: Commit**

```bash
git add backend/src/MyMemo.Shared/Database/Turso/TursoDataReader.cs \
        backend/tests/MyMemo.Shared.Tests/Database/TursoDataReaderTests.cs
git commit -m "feat(turso): add TursoDataReader with unit tests"
```

---

## Task 4: `TursoDbCommand` with unit tests

**Files:**
- Create: `backend/src/MyMemo.Shared/Database/Turso/TursoDbCommand.cs`
- Create: `backend/tests/MyMemo.Shared.Tests/Database/TursoDbCommandTests.cs`

**Step 1: Write failing tests**

Create `backend/tests/MyMemo.Shared.Tests/Database/TursoDbCommandTests.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MyMemo.Shared.Database.Turso;

namespace MyMemo.Shared.Tests.Database;

public class TursoDbCommandTests
{
    private const string BaseUrl = "https://test-db.turso.io";
    private const string Token = "test-token";

    private static (TursoDbConnection conn, List<HttpRequestMessage> captured) MakeConnection(
        string responseJson)
    {
        var captured = new List<HttpRequestMessage>();
        var handler = new FakeHttpHandler(responseJson, captured);
        var http = new HttpClient(handler);
        var conn = new TursoDbConnection(BaseUrl, Token, http);
        return (conn, captured);
    }

    private static string MakeOkResponse(int affectedRows = 0, string? lastInsertId = null,
        string[][]? colNames = null, string[][]? rows = null)
    {
        var cols = colNames?.Select(c => $"{{\"name\":\"{c[0]}\",\"decltype\":\"{c[1]}\"}}") ?? [];
        var rowsJson = rows?.Select(r =>
            "[" + string.Join(",", r.Select(v => $"{{\"type\":\"text\",\"value\":\"{v}\"}}")) + "]") ?? [];

        return $$"""
        {
          "results": [
            {
              "type": "ok",
              "response": {
                "type": "execute",
                "result": {
                  "cols": [{{string.Join(",", cols)}}],
                  "rows": [{{string.Join(",", rowsJson)}}],
                  "affected_row_count": {{affectedRows}},
                  "last_insert_rowid": {{(lastInsertId is null ? "null" : $"\"{lastInsertId}\"")}}
                }
              }
            },
            {"type":"ok","response":{"type":"close"}}
          ]
        }
        """;
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_ReturnsAffectedRowCount()
    {
        var (conn, _) = MakeConnection(MakeOkResponse(affectedRows: 1));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO sessions (id) VALUES (@id)";
        cmd.Parameters.Add(new TursoDbParameter { ParameterName = "id", Value = "abc" });

        var result = await cmd.ExecuteNonQueryAsync();

        result.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteScalarAsync_ReturnsFirstColumnOfFirstRow()
    {
        var (conn, _) = MakeConnection(MakeOkResponse(
            colNames: [["count(*)", "INTEGER"]],
            rows: [["42"]]));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM sessions";

        var result = await cmd.ExecuteScalarAsync();

        result.Should().Be(42L);
    }

    [Fact]
    public async Task ExecuteReaderAsync_ReturnsRowsViaDataReader()
    {
        var (conn, _) = MakeConnection(MakeOkResponse(
            colNames: [["id", "TEXT"], ["title", "TEXT"]],
            rows: [["abc123", "My Session"]]));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, title FROM sessions";

        using var reader = await cmd.ExecuteReaderAsync();
        reader.Read().Should().BeTrue();
        reader.GetString(0).Should().Be("abc123");
        reader.GetString(1).Should().Be("My Session");
        reader.Read().Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_SendsCorrectAuthHeader()
    {
        var (conn, captured) = MakeConnection(MakeOkResponse());
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM sessions";

        await cmd.ExecuteNonQueryAsync();

        captured.Should().HaveCount(1);
        captured[0].Headers.Authorization!.Scheme.Should().Be("Bearer");
        captured[0].Headers.Authorization!.Parameter.Should().Be(Token);
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_SendsNamedArgsWithoutPrefix()
    {
        var (conn, captured) = MakeConnection(MakeOkResponse());
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE sessions SET status = @status WHERE id = @id";
        cmd.Parameters.Add(new TursoDbParameter { ParameterName = "status", Value = "done" });
        cmd.Parameters.Add(new TursoDbParameter { ParameterName = "id", Value = "abc" });

        await cmd.ExecuteNonQueryAsync();

        var body = await captured[0].Content!.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var namedArgs = doc.RootElement
            .GetProperty("requests")[0]
            .GetProperty("stmt")
            .GetProperty("named_args");

        namedArgs.GetArrayLength().Should().Be(2);
        namedArgs[0].GetProperty("name").GetString().Should().Be("status");
        namedArgs[1].GetProperty("name").GetString().Should().Be("id");
    }
}

file sealed class FakeHttpHandler(string responseJson, List<HttpRequestMessage> captured)
    : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        captured.Add(request);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };
    }
}
```

**Step 2: Run to confirm it fails**

```bash
cd backend && dotnet test tests/MyMemo.Shared.Tests --filter "TursoDbCommandTests" --verbosity normal
```

Expected: compile error — `TursoDbCommand` / `TursoDbConnection` not defined yet.

**Step 3: Implement `TursoDbCommand.cs`**

```csharp
using System.Data;
using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyMemo.Shared.Database.Turso;

public sealed class TursoDbCommand(TursoDbConnection connection) : DbCommand
{
    private readonly TursoDbParameterCollection _params = new();

    public override string CommandText { get; set; } = "";
    public override int CommandTimeout { get; set; } = 30;
    public override CommandType CommandType { get; set; } = CommandType.Text;
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection
    {
        get => connection;
        set { }
    }

    protected override DbParameterCollection DbParameterCollection => _params;
    protected override DbTransaction? DbTransaction { get; set; }

    protected override DbParameter CreateDbParameter() => new TursoDbParameter();

    public override void Cancel() { }
    public override void Prepare() { }
    public override int ExecuteNonQuery() => ExecuteNonQueryAsync().GetAwaiter().GetResult();
    public override object? ExecuteScalar() => ExecuteScalarAsync().GetAwaiter().GetResult();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
        ExecuteDbDataReaderAsync(behavior, CancellationToken.None).GetAwaiter().GetResult();

    protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(
        CommandBehavior behavior, CancellationToken cancellationToken)
    {
        var result = await SendAsync(cancellationToken);
        return new TursoDataReader(result.Cols, result.Rows);
    }

    public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
    {
        var result = await SendAsync(cancellationToken);
        return (int)result.AffectedRowCount;
    }

    public override async Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
    {
        var result = await SendAsync(cancellationToken);
        if (result.Rows.Count == 0 || result.Cols.Count == 0) return null;
        var v = result.Rows[0][0];
        if (v.Type == "null" || v.Value is null) return DBNull.Value;
        return v.Type switch
        {
            "integer" => long.TryParse(v.Value, out var l) ? (object)l : v.Value,
            "real"    => double.TryParse(v.Value,
                             System.Globalization.NumberStyles.Any,
                             System.Globalization.CultureInfo.InvariantCulture, out var d) ? (object)d : v.Value,
            _         => v.Value
        };
    }

    private async Task<ExecuteResult> SendAsync(CancellationToken ct)
    {
        var namedArgs = _params.All
            .Select(p =>
            {
                var name = p.ParameterName.TrimStart('@', ':', '$');
                var (type, value) = ToTursoValue(p.Value);
                return new JsonObject
                {
                    ["name"] = name,
                    ["value"] = new JsonObject { ["type"] = type, ["value"] = value }
                };
            })
            .ToList();

        var stmt = new JsonObject { ["sql"] = CommandText };
        if (namedArgs.Count > 0)
            stmt["named_args"] = new JsonArray(namedArgs.Cast<JsonNode?>().ToArray());

        var body = new JsonObject
        {
            ["requests"] = new JsonArray(
                new JsonObject { ["type"] = "execute", ["stmt"] = stmt },
                new JsonObject { ["type"] = "close" }
            )
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, connection.PipelineUrl)
        {
            Content = JsonContent.Create(body),
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", connection.AuthToken) }
        };

        using var resp = await connection.HttpClient.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var resultEl = doc.RootElement.GetProperty("results")[0];

        if (resultEl.GetProperty("type").GetString() == "error")
        {
            var msg = resultEl.GetProperty("error").GetProperty("message").GetString();
            throw new InvalidOperationException($"Turso error: {msg}");
        }

        var execResult = resultEl.GetProperty("response").GetProperty("result");
        var cols = execResult.GetProperty("cols").EnumerateArray()
            .Select(c => new TursoCol(
                c.GetProperty("name").GetString()!,
                c.TryGetProperty("decltype", out var dt) ? dt.GetString() ?? "TEXT" : "TEXT"))
            .ToList();

        var rows = execResult.GetProperty("rows").EnumerateArray()
            .Select(row => row.EnumerateArray()
                .Select(v => new TursoValue(
                    v.GetProperty("type").GetString()!,
                    v.TryGetProperty("value", out var val) && val.ValueKind != JsonValueKind.Null
                        ? val.GetString()
                        : null))
                .ToArray())
            .ToList();

        var affected = execResult.GetProperty("affected_row_count").GetInt64();
        return new ExecuteResult(cols, rows, affected);
    }

    private static (string Type, JsonNode? Value) ToTursoValue(object? v) => v switch
    {
        null or DBNull  => ("null", null),
        string s        => ("text", JsonValue.Create(s)),
        bool b          => ("integer", JsonValue.Create(b ? 1 : 0)),
        int i           => ("integer", JsonValue.Create((long)i)),
        long l          => ("integer", JsonValue.Create(l)),
        float f         => ("real", JsonValue.Create((double)f)),
        double d        => ("real", JsonValue.Create(d)),
        decimal dec     => ("real", JsonValue.Create((double)dec)),
        _               => ("text", JsonValue.Create(v.ToString()))
    };

    private record ExecuteResult(List<TursoCol> Cols, List<TursoValue[]> Rows, long AffectedRowCount);
}
```

**Step 4: Run tests to confirm they pass**

```bash
cd backend && dotnet test tests/MyMemo.Shared.Tests --filter "TursoDbCommandTests" --verbosity normal
```

Expected: all 5 pass.

**Step 5: Commit**

```bash
git add backend/src/MyMemo.Shared/Database/Turso/TursoDbCommand.cs \
        backend/tests/MyMemo.Shared.Tests/Database/TursoDbCommandTests.cs
git commit -m "feat(turso): add TursoDbCommand with unit tests"
```

---

## Task 5: `TursoDbConnection` and `TursoConnectionFactory`

**Files:**
- Create: `backend/src/MyMemo.Shared/Database/Turso/TursoDbConnection.cs`
- Create: `backend/src/MyMemo.Shared/Database/Turso/TursoConnectionFactory.cs`

No new tests here — `TursoDbConnection` is pure plumbing, covered by the integration test in Task 6.

**Step 1: Create `TursoDbConnection.cs`**

```csharp
using System.Data;
using System.Data.Common;

namespace MyMemo.Shared.Database.Turso;

public sealed class TursoDbConnection(string baseUrl, string authToken, HttpClient? httpClient = null)
    : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;

    internal HttpClient HttpClient { get; } = httpClient ?? new HttpClient();
    internal string AuthToken { get; } = authToken;
    internal string PipelineUrl { get; } = NormalizeUrl(baseUrl);

    public override string ConnectionString { get; set; } = baseUrl;
    public override string Database => "";
    public override string DataSource => baseUrl;
    public override string ServerVersion => "turso";
    public override ConnectionState State => _state;

    public override void Open()
    {
        _state = ConnectionState.Open;
    }

    public override async Task OpenAsync(CancellationToken cancellationToken)
    {
        _state = ConnectionState.Open;
        await Task.CompletedTask;
    }

    public override void Close() => _state = ConnectionState.Closed;

    public override void ChangeDatabase(string databaseName) { }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        throw new NotSupportedException("Turso does not support ADO.NET transactions via this driver. Use Batch API.");

    protected override DbCommand CreateDbCommand() => new TursoDbCommand(this);

    protected override void Dispose(bool disposing)
    {
        if (disposing) _state = ConnectionState.Closed;
        base.Dispose(disposing);
    }

    private static string NormalizeUrl(string url)
    {
        var http = url
            .Replace("libsql://", "https://", StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');
        return $"{http}/v2/pipeline";
    }
}
```

**Step 2: Create `TursoConnectionFactory.cs`**

```csharp
using System.Data.Common;

namespace MyMemo.Shared.Database.Turso;

public sealed class TursoConnectionFactory(string url, string authToken) : IDbConnectionFactory
{
    public Task<DbConnection> CreateConnectionAsync()
    {
        var conn = new TursoDbConnection(url, authToken);
        conn.Open();
        return Task.FromResult<DbConnection>(conn);
    }
}
```

**Step 3: Build**

```bash
cd backend && dotnet build src/MyMemo.Shared --no-restore
```

Expected: 0 errors.

**Step 4: Commit**

```bash
git add backend/src/MyMemo.Shared/Database/Turso/
git commit -m "feat(turso): add TursoDbConnection and TursoConnectionFactory"
```

---

## Task 6: Integration test — repositories work via Turso

**Files:**
- Create: `backend/tests/MyMemo.Shared.Tests/Database/TursoIntegrationTests.cs`

This test skips automatically if `TURSO_URL` and `TURSO_AUTH_TOKEN` env vars are not set. Run it locally or in CI against a real Turso dev database.

**Step 1: Write the integration test**

Create `backend/tests/MyMemo.Shared.Tests/Database/TursoIntegrationTests.cs`:

```csharp
using FluentAssertions;
using MyMemo.Shared.Database;
using MyMemo.Shared.Database.Turso;
using MyMemo.Shared.Repositories;

namespace MyMemo.Shared.Tests.Database;

public class TursoIntegrationTests
{
    private readonly IDbConnectionFactory? _factory;
    private readonly bool _skip;

    public TursoIntegrationTests()
    {
        var url = Environment.GetEnvironmentVariable("TURSO_URL");
        var token = Environment.GetEnvironmentVariable("TURSO_AUTH_TOKEN");
        _skip = string.IsNullOrEmpty(url) || string.IsNullOrEmpty(token);
        if (!_skip)
        {
            _factory = new TursoConnectionFactory(url!, token!);
            DatabaseInitializer.Initialize(_factory).GetAwaiter().GetResult();
        }
    }

    [Fact]
    public async Task UserRepository_CreateAndRetrieve()
    {
        if (_skip) return;

        var repo = new UserRepository(_factory!);
        var clerkId = $"test_{Guid.NewGuid():N}";
        var user = await repo.GetOrCreateByClerkIdAsync(clerkId, "test@test.com", "Test User");

        user.Should().NotBeNull();
        user.ClerkId.Should().Be(clerkId);

        var same = await repo.GetOrCreateByClerkIdAsync(clerkId, "test@test.com", "Test User");
        same.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task SessionRepository_CreateListDelete()
    {
        if (_skip) return;

        var users = new UserRepository(_factory!);
        var sessions = new SessionRepository(_factory!);

        var clerkId = $"test_{Guid.NewGuid():N}";
        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "test@test.com", "Test");

        var session = await sessions.CreateAsync(user.Id, "full", "microphone");
        session.Should().NotBeNull();
        session.Status.Should().Be("recording");

        var found = await sessions.GetByIdAsync(session.Id);
        found.Should().NotBeNull();
        found!.UserId.Should().Be(user.Id);

        var list = await sessions.ListByUserAsync(user.Id);
        list.Should().Contain(s => s.Id == session.Id);

        await sessions.DeleteAsync(session.Id);
        var gone = await sessions.GetByIdAsync(session.Id);
        gone.Should().BeNull();
    }

    [Fact]
    public async Task ChunkRepository_CreateAndCount()
    {
        if (_skip) return;

        var users = new UserRepository(_factory!);
        var sessions = new SessionRepository(_factory!);
        var chunks = new ChunkRepository(_factory!);

        var clerkId = $"test_{Guid.NewGuid():N}";
        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "test@test.com", "Test");
        var session = await sessions.CreateAsync(user.Id, "full", "microphone");

        var chunk = await chunks.CreateAsync(session.Id, 0, $"blobs/{session.Id}/0.webm");
        chunk.Should().NotBeNull();

        var count = await chunks.CountBySessionAsync(session.Id);
        count.Should().Be(1);

        // cleanup
        await sessions.DeleteAsync(session.Id);
    }
}
```

**Step 2: Run without env vars to confirm skip behaviour**

```bash
cd backend && dotnet test tests/MyMemo.Shared.Tests --filter "TursoIntegrationTests" --verbosity normal
```

Expected: 3 tests pass (they silently return when `_skip = true`).

**Step 3: (Optional) Run with real Turso credentials**

```bash
TURSO_URL="libsql://your-db.turso.io" \
TURSO_AUTH_TOKEN="your-token" \
dotnet test tests/MyMemo.Shared.Tests --filter "TursoIntegrationTests" --verbosity normal
```

Expected: all 3 pass against real Turso.

**Step 4: Commit**

```bash
git add backend/tests/MyMemo.Shared.Tests/Database/TursoIntegrationTests.cs
git commit -m "test(turso): add integration tests for Turso repositories"
```

---

## Task 7: Fix `DatabaseInitializer` PRAGMAs

**Files:**
- Modify: `backend/src/MyMemo.Shared/Database/DatabaseInitializer.cs`

`PRAGMA journal_mode=WAL` and `PRAGMA foreign_keys=ON` are no-ops or errors on Turso. Wrap them in try-catch so the initializer works for both backends.

**Step 1: Update `DatabaseInitializer.cs`**

Change lines 9-10 from:
```csharp
await connection.ExecuteAsync("PRAGMA journal_mode=WAL;");
await connection.ExecuteAsync("PRAGMA foreign_keys=ON;");
```

To:
```csharp
try { await connection.ExecuteAsync("PRAGMA journal_mode=WAL;"); } catch { /* not supported on Turso */ }
try { await connection.ExecuteAsync("PRAGMA foreign_keys=ON;"); } catch { /* not supported on Turso */ }
```

**Step 2: Run all shared tests**

```bash
cd backend && dotnet test tests/MyMemo.Shared.Tests --verbosity normal
```

Expected: all green.

**Step 3: Commit**

```bash
git add backend/src/MyMemo.Shared/Database/DatabaseInitializer.cs
git commit -m "fix(db): wrap PRAGMA statements in try-catch for Turso compatibility"
```

---

## Task 8: Wire factory selection in API and Worker `Program.cs`

**Files:**
- Modify: `backend/src/MyMemo.Api/Program.cs`
- Modify: `backend/src/MyMemo.Worker/Program.cs`

**Step 1: Update `MyMemo.Api/Program.cs`**

Replace the existing database section (lines 28-38):

```csharp
// Database — use Turso in production, SQLite locally
var tursoUrl   = builder.Configuration["Turso:Url"];
var tursoToken = builder.Configuration["Turso:AuthToken"];

IDbConnectionFactory dbFactory = !string.IsNullOrWhiteSpace(tursoUrl)
    ? new TursoConnectionFactory(tursoUrl, tursoToken!)
    : new SqliteConnectionFactory(builder.Configuration.GetConnectionString("Database") ?? "Data Source=mymemo.db");
builder.Services.AddSingleton(dbFactory);

// Keep a connection alive for in-memory SQLite (shared cache requires one open connection)
Microsoft.Data.Sqlite.SqliteConnection? keepAliveConnection = null;
if (string.IsNullOrWhiteSpace(tursoUrl))
{
    var localCs = builder.Configuration.GetConnectionString("Database") ?? "Data Source=mymemo.db";
    if (localCs.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase))
    {
        keepAliveConnection = new Microsoft.Data.Sqlite.SqliteConnection(localCs);
        keepAliveConnection.Open();
    }
}
```

Also add the using at the top:
```csharp
using MyMemo.Shared.Database.Turso;
```

And change `AddSingleton<IDbConnectionFactory>` to `AddSingleton(dbFactory)` (already done above).

**Step 2: Update `MyMemo.Worker/Program.cs`**

Replace the database section (lines 10-12):

```csharp
using MyMemo.Shared.Database.Turso;

// ...

// Database — use Turso in production, SQLite locally
var tursoUrl   = builder.Configuration["Turso:Url"];
var tursoToken = builder.Configuration["Turso:AuthToken"];

IDbConnectionFactory dbFactory = !string.IsNullOrWhiteSpace(tursoUrl)
    ? new TursoConnectionFactory(tursoUrl, tursoToken!)
    : new SqliteConnectionFactory(builder.Configuration.GetConnectionString("Database") ?? "Data Source=mymemo.db");
builder.Services.AddSingleton(dbFactory);
```

**Step 3: Build both projects**

```bash
cd backend && dotnet build src/MyMemo.Api src/MyMemo.Worker --no-restore
```

Expected: 0 errors.

**Step 4: Run all tests**

```bash
cd backend && dotnet test --verbosity normal
```

Expected: all green.

**Step 5: Commit**

```bash
git add backend/src/MyMemo.Api/Program.cs backend/src/MyMemo.Worker/Program.cs
git commit -m "feat(turso): wire Turso factory into API and Worker based on config"
```

---

## Task 9: Deploy and verify end-to-end

**Step 1: Push to trigger CI/CD**

```bash
git push
```

This triggers `backend-deploy.yml` which builds Docker images and updates both Container Apps.

**Step 2: Confirm `Turso__Url` and `Turso__AuthToken` are set on both Container Apps**

```bash
az containerapp show --name mymemo-api --resource-group mymemo-rg \
  --query "properties.template.containers[0].env[?name=='Turso__Url']" -o tsv

az containerapp show --name mymemo-worker --resource-group mymemo-rg \
  --query "properties.template.containers[0].env[?name=='Turso__Url']" -o tsv
```

If empty, set them:
```bash
az containerapp update --name mymemo-api --resource-group mymemo-rg \
  --set-env-vars "Turso__Url=secretref:turso-url" "Turso__AuthToken=secretref:turso-auth-token"

az containerapp update --name mymemo-worker --resource-group mymemo-rg \
  --set-env-vars "Turso__Url=secretref:turso-url" "Turso__AuthToken=secretref:turso-auth-token"
```

**Step 3: Smoke test via health endpoint**

```bash
curl https://mymemo-api.mangowave-526aa429.westeurope.azurecontainerapps.io/health
```

Expected: `{"status":"healthy"}`

**Step 4: Watch worker logs after a recording session**

```bash
az containerapp logs show --name mymemo-worker --resource-group mymemo-rg --tail 50 --follow false
```

Expected: no `FOREIGN KEY constraint failed` — transcription and memo generation succeed.
