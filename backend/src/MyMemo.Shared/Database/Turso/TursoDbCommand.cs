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
        var col = result.Cols[0];
        if (v.Type == "null" || v.Value is null) return DBNull.Value;
        // Coerce by wire type first, then fall back to declared column type
        if (v.Type == "integer" || col.DeclType.Equals("INTEGER", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(v.Value, out var l) ? (object)l : v.Value;
        if (v.Type == "real" || col.DeclType.Equals("REAL", StringComparison.OrdinalIgnoreCase))
            return double.TryParse(v.Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? (object)d : v.Value;
        return v.Value;
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

        var req = new HttpRequestMessage(HttpMethod.Post, connection.PipelineUrl)
        {
            Content = JsonContent.Create(body),
            Headers =
            {
                Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer", connection.AuthToken)
            }
        };

        using var resp = await connection.HttpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var errorBody = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Turso HTTP {(int)resp.StatusCode}: {errorBody}");
        }

        using var doc = await JsonDocument.ParseAsync(
            await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
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
        bool b          => ("integer", JsonValue.Create(b ? "1" : "0")),
        int i           => ("integer", JsonValue.Create(i.ToString())),
        long l          => ("integer", JsonValue.Create(l.ToString())),
        float f         => ("real", JsonValue.Create(((double)f).ToString(System.Globalization.CultureInfo.InvariantCulture))),
        double d        => ("real", JsonValue.Create(d.ToString(System.Globalization.CultureInfo.InvariantCulture))),
        decimal dec     => ("real", JsonValue.Create(((double)dec).ToString(System.Globalization.CultureInfo.InvariantCulture))),
        _               => ("text", JsonValue.Create(v.ToString()))
    };

    private record ExecuteResult(List<TursoCol> Cols, List<TursoValue[]> Rows, long AffectedRowCount);
}
