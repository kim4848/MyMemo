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

        return $$$"""
        {
          "results": [
            {
              "type": "ok",
              "response": {
                "type": "execute",
                "result": {
                  "cols": [{{{string.Join(",", cols)}}}],
                  "rows": [{{{string.Join(",", rowsJson)}}}],
                  "affected_row_count": {{{affectedRows}}},
                  "last_insert_rowid": {{{(lastInsertId is null ? "null" : $"\"{lastInsertId}\"")}}}
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
