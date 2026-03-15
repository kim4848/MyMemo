using Dapper;
using FluentAssertions;
using MyMemo.Shared.Database;

namespace MyMemo.Shared.Tests.Database;

public class DatabaseInitializerTests
{
    [Fact]
    public async Task Initialize_CreatesAllTables_WithSqlite()
    {
        using var fixture = new Repositories.TestDbFixture();
        using var conn = await fixture.Factory.CreateConnectionAsync();

        var tables = (await conn.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'"))
            .ToList();

        tables.Should().Contain("users");
        tables.Should().Contain("sessions");
        tables.Should().Contain("chunks");
        tables.Should().Contain("transcriptions");
        tables.Should().Contain("memos");
        tables.Should().Contain("tags");
        tables.Should().Contain("session_tags");
        tables.Should().Contain("batch_transcription_jobs");
        tables.Should().Contain("infographics");
    }

    [Fact]
    public async Task Initialize_IsIdempotent_WithSqlite()
    {
        using var fixture = new Repositories.TestDbFixture();

        // Running initialize again should not throw
        var act = () => DatabaseInitializer.Initialize(fixture.Factory);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Initialize_CreatesIndexes_WithSqlite()
    {
        using var fixture = new Repositories.TestDbFixture();
        using var conn = await fixture.Factory.CreateConnectionAsync();

        var indexes = (await conn.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type='index' AND name NOT LIKE 'sqlite_%'"))
            .ToList();

        indexes.Should().Contain("idx_sessions_user");
        indexes.Should().Contain("idx_chunks_session");
        indexes.Should().Contain("idx_tags_user");
        indexes.Should().Contain("idx_session_tags_session");
        indexes.Should().Contain("idx_session_tags_tag");
    }

    [Fact]
    public async Task Initialize_MigrationColumns_Exist_WithSqlite()
    {
        using var fixture = new Repositories.TestDbFixture();
        using var conn = await fixture.Factory.CreateConnectionAsync();

        // Verify migration-added columns are present by inserting/querying them
        var columns = (await conn.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('sessions')")).ToList();

        columns.Should().Contain("memo_queued");
        columns.Should().Contain("context");
        columns.Should().Contain("transcription_mode");

        var memoColumns = (await conn.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('memos')")).ToList();

        memoColumns.Should().Contain("generation_duration_ms");
        memoColumns.Should().Contain("updated_at");

        var transcriptionColumns = (await conn.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('transcriptions')")).ToList();

        transcriptionColumns.Should().Contain("transcription_duration_ms");
    }
}
