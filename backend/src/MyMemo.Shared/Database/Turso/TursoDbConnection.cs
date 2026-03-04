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

    public override void Open() => _state = ConnectionState.Open;

    public override async Task OpenAsync(CancellationToken cancellationToken)
    {
        _state = ConnectionState.Open;
        await Task.CompletedTask;
    }

    public override void Close() => _state = ConnectionState.Closed;

    public override void ChangeDatabase(string databaseName) { }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        throw new NotSupportedException("Turso does not support ADO.NET transactions via this driver.");

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
