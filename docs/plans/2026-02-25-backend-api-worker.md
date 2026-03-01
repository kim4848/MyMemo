# Backend API & Worker Service Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build the .NET 8 backend — API service with all V1 endpoints + worker service for transcription and memo generation.

**Architecture:** Two .NET 8 projects (API + Worker) sharing a common library. API handles HTTP requests, uploads audio to Blob Storage, enqueues jobs on Service Bus. Worker consumes queue messages, calls Whisper for transcription and GPT-4.1 Nano for memo generation. All data stored in Turso (SQLite).

**Tech Stack:** .NET 8 Minimal API, Dapper + Microsoft.Data.Sqlite (dev) / Turso (prod), Azure Blob Storage, Azure Service Bus, Azure OpenAI (Whisper + GPT-4.1 Nano), Clerk JWT auth, xUnit + NSubstitute + FluentAssertions

---

## Solution Structure

```
backend/
├── MyMemo.sln
├── Directory.Build.props
├── src/
│   ├── MyMemo.Api/
│   │   ├── MyMemo.Api.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── Auth/
│   │   │   └── ClerkAuthExtensions.cs
│   │   └── Endpoints/
│   │       ├── SessionEndpoints.cs
│   │       ├── ChunkEndpoints.cs
│   │       └── MemoEndpoints.cs
│   ├── MyMemo.Worker/
│   │   ├── MyMemo.Worker.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── Workers/
│   │       ├── TranscriptionWorker.cs
│   │       └── MemoGenerationWorker.cs
│   └── MyMemo.Shared/
│       ├── MyMemo.Shared.csproj
│       ├── Models/
│       │   ├── User.cs
│       │   ├── Session.cs
│       │   ├── Chunk.cs
│       │   ├── Transcription.cs
│       │   └── Memo.cs
│       ├── Database/
│       │   ├── IDbConnectionFactory.cs
│       │   ├── SqliteConnectionFactory.cs
│       │   └── schema.sql
│       ├── Repositories/
│       │   ├── IUserRepository.cs
│       │   ├── UserRepository.cs
│       │   ├── ISessionRepository.cs
│       │   ├── SessionRepository.cs
│       │   ├── IChunkRepository.cs
│       │   ├── ChunkRepository.cs
│       │   ├── ITranscriptionRepository.cs
│       │   ├── TranscriptionRepository.cs
│       │   ├── IMemoRepository.cs
│       │   └── MemoRepository.cs
│       └── Services/
│           ├── IBlobStorageService.cs
│           ├── BlobStorageService.cs
│           ├── IQueueService.cs
│           ├── QueueService.cs
│           ├── IWhisperService.cs
│           ├── WhisperService.cs
│           ├── IMemoGeneratorService.cs
│           └── MemoGeneratorService.cs
└── tests/
    ├── MyMemo.Api.Tests/
    │   ├── MyMemo.Api.Tests.csproj
    │   └── Endpoints/
    │       ├── SessionEndpointsTests.cs
    │       ├── ChunkEndpointsTests.cs
    │       └── MemoEndpointsTests.cs
    ├── MyMemo.Worker.Tests/
    │   ├── MyMemo.Worker.Tests.csproj
    │   └── Workers/
    │       ├── TranscriptionWorkerTests.cs
    │       └── MemoGenerationWorkerTests.cs
    └── MyMemo.Shared.Tests/
        ├── MyMemo.Shared.Tests.csproj
        └── Repositories/
            ├── UserRepositoryTests.cs
            ├── SessionRepositoryTests.cs
            └── ChunkRepositoryTests.cs
```

---

## Task 1: Scaffold .NET Solution & Projects

**Files:**
- Create: `backend/MyMemo.sln`
- Create: `backend/Directory.Build.props`
- Create: `backend/src/MyMemo.Shared/MyMemo.Shared.csproj`
- Create: `backend/src/MyMemo.Api/MyMemo.Api.csproj`
- Create: `backend/src/MyMemo.Worker/MyMemo.Worker.csproj`
- Create: `backend/tests/MyMemo.Shared.Tests/MyMemo.Shared.Tests.csproj`
- Create: `backend/tests/MyMemo.Api.Tests/MyMemo.Api.Tests.csproj`
- Create: `backend/tests/MyMemo.Worker.Tests/MyMemo.Worker.Tests.csproj`
- Create: `backend/src/MyMemo.Api/Program.cs`
- Create: `backend/src/MyMemo.Worker/Program.cs`

**Step 1: Create solution and projects**

```bash
cd backend
dotnet new sln -n MyMemo
mkdir -p src/MyMemo.Shared src/MyMemo.Api src/MyMemo.Worker
mkdir -p tests/MyMemo.Shared.Tests tests/MyMemo.Api.Tests tests/MyMemo.Worker.Tests
dotnet new classlib -n MyMemo.Shared -o src/MyMemo.Shared
dotnet new web -n MyMemo.Api -o src/MyMemo.Api
dotnet new worker -n MyMemo.Worker -o src/MyMemo.Worker
dotnet new xunit -n MyMemo.Shared.Tests -o tests/MyMemo.Shared.Tests
dotnet new xunit -n MyMemo.Api.Tests -o tests/MyMemo.Api.Tests
dotnet new xunit -n MyMemo.Worker.Tests -o tests/MyMemo.Worker.Tests
dotnet sln add src/MyMemo.Shared src/MyMemo.Api src/MyMemo.Worker
dotnet sln add tests/MyMemo.Shared.Tests tests/MyMemo.Api.Tests tests/MyMemo.Worker.Tests
```

**Step 2: Create Directory.Build.props**

```xml
<!-- backend/Directory.Build.props -->
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

**Step 3: Add NuGet packages**

```bash
cd backend

# Shared
dotnet add src/MyMemo.Shared package Dapper
dotnet add src/MyMemo.Shared package Microsoft.Data.Sqlite
dotnet add src/MyMemo.Shared package Azure.Storage.Blobs
dotnet add src/MyMemo.Shared package Azure.Messaging.ServiceBus
dotnet add src/MyMemo.Shared package Azure.AI.OpenAI --prerelease
dotnet add src/MyMemo.Shared package Microsoft.Extensions.Options

# API
dotnet add src/MyMemo.Api reference src/MyMemo.Shared
dotnet add src/MyMemo.Api package Microsoft.AspNetCore.Authentication.JwtBearer

# Worker
dotnet add src/MyMemo.Worker reference src/MyMemo.Shared

# Test projects
dotnet add tests/MyMemo.Shared.Tests reference src/MyMemo.Shared
dotnet add tests/MyMemo.Shared.Tests package NSubstitute
dotnet add tests/MyMemo.Shared.Tests package FluentAssertions

dotnet add tests/MyMemo.Api.Tests reference src/MyMemo.Api
dotnet add tests/MyMemo.Api.Tests package NSubstitute
dotnet add tests/MyMemo.Api.Tests package FluentAssertions
dotnet add tests/MyMemo.Api.Tests package Microsoft.AspNetCore.Mvc.Testing

dotnet add tests/MyMemo.Worker.Tests reference src/MyMemo.Worker
dotnet add tests/MyMemo.Worker.Tests package NSubstitute
dotnet add tests/MyMemo.Worker.Tests package FluentAssertions
```

**Step 4: Write minimal Program.cs for API**

```csharp
// backend/src/MyMemo.Api/Program.cs
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

public partial class Program { }
```

**Step 5: Write minimal Program.cs for Worker**

```csharp
// backend/src/MyMemo.Worker/Program.cs
var builder = Host.CreateApplicationBuilder(args);
var host = builder.Build();
host.Run();
```

**Step 6: Verify solution builds and tests pass**

```bash
cd backend
dotnet build
dotnet test
```

Expected: BUILD SUCCEEDED, all default tests pass.

**Step 7: Commit**

```bash
git add backend/
git commit -m "feat(backend): scaffold .NET 8 solution with API, Worker, and Shared projects"
```

---

## Task 2: Database Schema & Models

**Files:**
- Create: `backend/src/MyMemo.Shared/Database/schema.sql`
- Create: `backend/src/MyMemo.Shared/Models/User.cs`
- Create: `backend/src/MyMemo.Shared/Models/Session.cs`
- Create: `backend/src/MyMemo.Shared/Models/Chunk.cs`
- Create: `backend/src/MyMemo.Shared/Models/Transcription.cs`
- Create: `backend/src/MyMemo.Shared/Models/Memo.cs`

**Step 1: Create schema.sql**

```sql
-- backend/src/MyMemo.Shared/Database/schema.sql
CREATE TABLE IF NOT EXISTS users (
    id          TEXT PRIMARY KEY DEFAULT (lower(hex(randomblob(16)))),
    email       TEXT NOT NULL UNIQUE,
    name        TEXT NOT NULL,
    clerk_id    TEXT NOT NULL UNIQUE,
    created_at  TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at  TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS sessions (
    id           TEXT PRIMARY KEY DEFAULT (lower(hex(randomblob(16)))),
    user_id      TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    title        TEXT,
    status       TEXT NOT NULL DEFAULT 'recording',
    output_mode  TEXT NOT NULL DEFAULT 'full',
    audio_source TEXT NOT NULL DEFAULT 'microphone',
    started_at   TEXT NOT NULL DEFAULT (datetime('now')),
    ended_at     TEXT,
    created_at   TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at   TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS idx_sessions_user ON sessions(user_id);

CREATE TABLE IF NOT EXISTS chunks (
    id            TEXT PRIMARY KEY DEFAULT (lower(hex(randomblob(16)))),
    session_id    TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    chunk_index   INTEGER NOT NULL,
    blob_path     TEXT NOT NULL,
    duration_sec  INTEGER,
    status        TEXT NOT NULL DEFAULT 'uploaded',
    error_message TEXT,
    created_at    TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at    TEXT NOT NULL DEFAULT (datetime('now')),
    UNIQUE(session_id, chunk_index)
);

CREATE INDEX IF NOT EXISTS idx_chunks_session ON chunks(session_id);

CREATE TABLE IF NOT EXISTS transcriptions (
    id              TEXT PRIMARY KEY DEFAULT (lower(hex(randomblob(16)))),
    chunk_id        TEXT NOT NULL UNIQUE REFERENCES chunks(id) ON DELETE CASCADE,
    raw_text        TEXT NOT NULL,
    language        TEXT DEFAULT 'da',
    confidence      REAL,
    word_timestamps TEXT,
    created_at      TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS memos (
    id                TEXT PRIMARY KEY DEFAULT (lower(hex(randomblob(16)))),
    session_id        TEXT NOT NULL UNIQUE REFERENCES sessions(id) ON DELETE CASCADE,
    output_mode       TEXT NOT NULL,
    content           TEXT NOT NULL,
    model_used        TEXT NOT NULL,
    prompt_tokens     INTEGER,
    completion_tokens INTEGER,
    created_at        TEXT NOT NULL DEFAULT (datetime('now'))
);
```

**Step 2: Create model classes**

```csharp
// backend/src/MyMemo.Shared/Models/User.cs
namespace MyMemo.Shared.Models;

public sealed class User
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public required string Name { get; init; }
    public required string ClerkId { get; init; }
    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
}
```

```csharp
// backend/src/MyMemo.Shared/Models/Session.cs
namespace MyMemo.Shared.Models;

public sealed class Session
{
    public required string Id { get; init; }
    public required string UserId { get; init; }
    public string? Title { get; init; }
    public required string Status { get; init; }
    public required string OutputMode { get; init; }
    public required string AudioSource { get; init; }
    public required string StartedAt { get; init; }
    public string? EndedAt { get; init; }
    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
}
```

```csharp
// backend/src/MyMemo.Shared/Models/Chunk.cs
namespace MyMemo.Shared.Models;

public sealed class Chunk
{
    public required string Id { get; init; }
    public required string SessionId { get; init; }
    public required int ChunkIndex { get; init; }
    public required string BlobPath { get; init; }
    public int? DurationSec { get; init; }
    public required string Status { get; init; }
    public string? ErrorMessage { get; init; }
    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
}
```

```csharp
// backend/src/MyMemo.Shared/Models/Transcription.cs
namespace MyMemo.Shared.Models;

public sealed class Transcription
{
    public required string Id { get; init; }
    public required string ChunkId { get; init; }
    public required string RawText { get; init; }
    public string Language { get; init; } = "da";
    public double? Confidence { get; init; }
    public string? WordTimestamps { get; init; }
    public required string CreatedAt { get; init; }
}
```

```csharp
// backend/src/MyMemo.Shared/Models/Memo.cs
namespace MyMemo.Shared.Models;

public sealed class Memo
{
    public required string Id { get; init; }
    public required string SessionId { get; init; }
    public required string OutputMode { get; init; }
    public required string Content { get; init; }
    public required string ModelUsed { get; init; }
    public int? PromptTokens { get; init; }
    public int? CompletionTokens { get; init; }
    public required string CreatedAt { get; init; }
}
```

**Step 3: Verify it builds**

```bash
cd backend && dotnet build
```

Expected: BUILD SUCCEEDED

**Step 4: Commit**

```bash
git add backend/src/MyMemo.Shared/Database/schema.sql backend/src/MyMemo.Shared/Models/
git commit -m "feat(backend): add database schema and entity models"
```

---

## Task 3: Database Connection Factory & Repositories

**Files:**
- Create: `backend/src/MyMemo.Shared/Database/IDbConnectionFactory.cs`
- Create: `backend/src/MyMemo.Shared/Database/SqliteConnectionFactory.cs`
- Create: `backend/src/MyMemo.Shared/Database/DatabaseInitializer.cs`
- Create: `backend/src/MyMemo.Shared/Repositories/IUserRepository.cs`
- Create: `backend/src/MyMemo.Shared/Repositories/UserRepository.cs`
- Create: `backend/src/MyMemo.Shared/Repositories/ISessionRepository.cs`
- Create: `backend/src/MyMemo.Shared/Repositories/SessionRepository.cs`
- Create: `backend/src/MyMemo.Shared/Repositories/IChunkRepository.cs`
- Create: `backend/src/MyMemo.Shared/Repositories/ChunkRepository.cs`
- Create: `backend/src/MyMemo.Shared/Repositories/ITranscriptionRepository.cs`
- Create: `backend/src/MyMemo.Shared/Repositories/TranscriptionRepository.cs`
- Create: `backend/src/MyMemo.Shared/Repositories/IMemoRepository.cs`
- Create: `backend/src/MyMemo.Shared/Repositories/MemoRepository.cs`
- Test: `backend/tests/MyMemo.Shared.Tests/Repositories/UserRepositoryTests.cs`
- Test: `backend/tests/MyMemo.Shared.Tests/Repositories/SessionRepositoryTests.cs`
- Test: `backend/tests/MyMemo.Shared.Tests/Repositories/ChunkRepositoryTests.cs`

**Step 1: Write failing test for UserRepository**

```csharp
// backend/tests/MyMemo.Shared.Tests/Repositories/UserRepositoryTests.cs
using FluentAssertions;
using Microsoft.Data.Sqlite;
using MyMemo.Shared.Database;
using MyMemo.Shared.Repositories;

namespace MyMemo.Shared.Tests.Repositories;

public class UserRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly UserRepository _sut;

    public UserRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var factory = new InMemoryConnectionFactory(_connection);
        DatabaseInitializer.Initialize(factory).GetAwaiter().GetResult();
        _sut = new UserRepository(factory);
    }

    [Fact]
    public async Task GetOrCreateByClerkId_CreatesNewUser_WhenNotExists()
    {
        var user = await _sut.GetOrCreateByClerkIdAsync("clerk_123", "test@example.com", "Test User");

        user.Should().NotBeNull();
        user.ClerkId.Should().Be("clerk_123");
        user.Email.Should().Be("test@example.com");
        user.Name.Should().Be("Test User");
        user.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetOrCreateByClerkId_ReturnsExistingUser_WhenExists()
    {
        var first = await _sut.GetOrCreateByClerkIdAsync("clerk_123", "test@example.com", "Test User");
        var second = await _sut.GetOrCreateByClerkIdAsync("clerk_123", "test@example.com", "Test User");

        second.Id.Should().Be(first.Id);
    }

    public void Dispose() => _connection.Dispose();
}

internal class InMemoryConnectionFactory(SqliteConnection connection) : IDbConnectionFactory
{
    public Task<SqliteConnection> CreateConnectionAsync() => Task.FromResult(connection);
}
```

**Step 2: Run test to verify it fails**

```bash
cd backend && dotnet test tests/MyMemo.Shared.Tests --filter "UserRepositoryTests" -v n
```

Expected: FAIL — types don't exist yet.

**Step 3: Implement IDbConnectionFactory and SqliteConnectionFactory**

```csharp
// backend/src/MyMemo.Shared/Database/IDbConnectionFactory.cs
using Microsoft.Data.Sqlite;

namespace MyMemo.Shared.Database;

public interface IDbConnectionFactory
{
    Task<SqliteConnection> CreateConnectionAsync();
}
```

```csharp
// backend/src/MyMemo.Shared/Database/SqliteConnectionFactory.cs
using Microsoft.Data.Sqlite;

namespace MyMemo.Shared.Database;

public sealed class SqliteConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public async Task<SqliteConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
```

```csharp
// backend/src/MyMemo.Shared/Database/DatabaseInitializer.cs
using Dapper;

namespace MyMemo.Shared.Database;

public static class DatabaseInitializer
{
    public static async Task Initialize(IDbConnectionFactory factory)
    {
        using var connection = await factory.CreateConnectionAsync();
        await connection.ExecuteAsync("PRAGMA journal_mode=WAL;");
        await connection.ExecuteAsync("PRAGMA foreign_keys=ON;");

        var schema = GetEmbeddedSchema();
        await connection.ExecuteAsync(schema);
    }

    private static string GetEmbeddedSchema()
    {
        var assembly = typeof(DatabaseInitializer).Assembly;
        using var stream = assembly.GetManifestResourceStream("MyMemo.Shared.Database.schema.sql")
            ?? throw new InvalidOperationException("schema.sql not found as embedded resource");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
```

Note: Add to `MyMemo.Shared.csproj`:
```xml
<ItemGroup>
  <EmbeddedResource Include="Database\schema.sql" />
</ItemGroup>
```

**Step 4: Implement UserRepository**

```csharp
// backend/src/MyMemo.Shared/Repositories/IUserRepository.cs
using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public interface IUserRepository
{
    Task<User> GetOrCreateByClerkIdAsync(string clerkId, string email, string name);
    Task<User?> GetByIdAsync(string id);
}
```

```csharp
// backend/src/MyMemo.Shared/Repositories/UserRepository.cs
using Dapper;
using MyMemo.Shared.Database;
using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public sealed class UserRepository(IDbConnectionFactory db) : IUserRepository
{
    public async Task<User> GetOrCreateByClerkIdAsync(string clerkId, string email, string name)
    {
        using var conn = await db.CreateConnectionAsync();
        var existing = await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT id, email, name, clerk_id AS ClerkId, created_at AS CreatedAt, updated_at AS UpdatedAt FROM users WHERE clerk_id = @clerkId",
            new { clerkId });

        if (existing is not null)
            return existing;

        var id = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            "INSERT INTO users (id, email, name, clerk_id, created_at, updated_at) VALUES (@id, @email, @name, @clerkId, @now, @now)",
            new { id, email, name, clerkId, now });

        return new User { Id = id, Email = email, Name = name, ClerkId = clerkId, CreatedAt = now, UpdatedAt = now };
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        using var conn = await db.CreateConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT id, email, name, clerk_id AS ClerkId, created_at AS CreatedAt, updated_at AS UpdatedAt FROM users WHERE id = @id",
            new { id });
    }
}
```

**Step 5: Run test to verify it passes**

```bash
cd backend && dotnet test tests/MyMemo.Shared.Tests --filter "UserRepositoryTests" -v n
```

Expected: PASS (2 tests)

**Step 6: Write failing tests for SessionRepository**

```csharp
// backend/tests/MyMemo.Shared.Tests/Repositories/SessionRepositoryTests.cs
using FluentAssertions;
using Microsoft.Data.Sqlite;
using MyMemo.Shared.Database;
using MyMemo.Shared.Repositories;

namespace MyMemo.Shared.Tests.Repositories;

public class SessionRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SessionRepository _sut;
    private readonly UserRepository _users;

    public SessionRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var factory = new InMemoryConnectionFactory(_connection);
        DatabaseInitializer.Initialize(factory).GetAwaiter().GetResult();
        _sut = new SessionRepository(factory);
        _users = new UserRepository(factory);
    }

    private async Task<string> CreateTestUser()
    {
        var user = await _users.GetOrCreateByClerkIdAsync("clerk_test", "test@test.com", "Test");
        return user.Id;
    }

    [Fact]
    public async Task CreateAsync_ReturnsNewSession()
    {
        var userId = await CreateTestUser();
        var session = await _sut.CreateAsync(userId, "full", "microphone");

        session.Should().NotBeNull();
        session.UserId.Should().Be(userId);
        session.Status.Should().Be("recording");
        session.OutputMode.Should().Be("full");
    }

    [Fact]
    public async Task ListByUserAsync_ReturnsOnlyUserSessions()
    {
        var userId = await CreateTestUser();
        await _sut.CreateAsync(userId, "full", "microphone");
        await _sut.CreateAsync(userId, "summary", "microphone");

        var sessions = await _sut.ListByUserAsync(userId);

        sessions.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _sut.GetByIdAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_RemovesSession()
    {
        var userId = await CreateTestUser();
        var session = await _sut.CreateAsync(userId, "full", "microphone");

        await _sut.DeleteAsync(session.Id);

        var result = await _sut.GetByIdAsync(session.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStatusAsync_ChangesStatus()
    {
        var userId = await CreateTestUser();
        var session = await _sut.CreateAsync(userId, "full", "microphone");

        await _sut.UpdateStatusAsync(session.Id, "processing");

        var updated = await _sut.GetByIdAsync(session.Id);
        updated!.Status.Should().Be("processing");
    }

    public void Dispose() => _connection.Dispose();
}
```

**Step 7: Run tests to verify they fail**

```bash
cd backend && dotnet test tests/MyMemo.Shared.Tests --filter "SessionRepositoryTests" -v n
```

Expected: FAIL — SessionRepository not implemented.

**Step 8: Implement SessionRepository**

```csharp
// backend/src/MyMemo.Shared/Repositories/ISessionRepository.cs
using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public interface ISessionRepository
{
    Task<Session> CreateAsync(string userId, string outputMode, string audioSource);
    Task<IReadOnlyList<Session>> ListByUserAsync(string userId);
    Task<Session?> GetByIdAsync(string id);
    Task DeleteAsync(string id);
    Task UpdateStatusAsync(string id, string status);
    Task SetEndedAtAsync(string id);
}
```

```csharp
// backend/src/MyMemo.Shared/Repositories/SessionRepository.cs
using Dapper;
using MyMemo.Shared.Database;
using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public sealed class SessionRepository(IDbConnectionFactory db) : ISessionRepository
{
    public async Task<Session> CreateAsync(string userId, string outputMode, string audioSource)
    {
        using var conn = await db.CreateConnectionAsync();
        var id = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            """
            INSERT INTO sessions (id, user_id, status, output_mode, audio_source, started_at, created_at, updated_at)
            VALUES (@id, @userId, 'recording', @outputMode, @audioSource, @now, @now, @now)
            """,
            new { id, userId, outputMode, audioSource, now });

        return (await GetByIdAsync(id))!;
    }

    public async Task<IReadOnlyList<Session>> ListByUserAsync(string userId)
    {
        using var conn = await db.CreateConnectionAsync();
        var results = await conn.QueryAsync<Session>(
            """
            SELECT id, user_id AS UserId, title, status, output_mode AS OutputMode,
                   audio_source AS AudioSource, started_at AS StartedAt, ended_at AS EndedAt,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM sessions WHERE user_id = @userId ORDER BY created_at DESC
            """,
            new { userId });
        return results.ToList();
    }

    public async Task<Session?> GetByIdAsync(string id)
    {
        using var conn = await db.CreateConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<Session>(
            """
            SELECT id, user_id AS UserId, title, status, output_mode AS OutputMode,
                   audio_source AS AudioSource, started_at AS StartedAt, ended_at AS EndedAt,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM sessions WHERE id = @id
            """,
            new { id });
    }

    public async Task DeleteAsync(string id)
    {
        using var conn = await db.CreateConnectionAsync();
        await conn.ExecuteAsync("DELETE FROM sessions WHERE id = @id", new { id });
    }

    public async Task UpdateStatusAsync(string id, string status)
    {
        using var conn = await db.CreateConnectionAsync();
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            "UPDATE sessions SET status = @status, updated_at = @now WHERE id = @id",
            new { id, status, now });
    }

    public async Task SetEndedAtAsync(string id)
    {
        using var conn = await db.CreateConnectionAsync();
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            "UPDATE sessions SET ended_at = @now, updated_at = @now WHERE id = @id",
            new { id, now });
    }
}
```

**Step 9: Run tests to verify they pass**

```bash
cd backend && dotnet test tests/MyMemo.Shared.Tests --filter "SessionRepositoryTests" -v n
```

Expected: PASS (5 tests)

**Step 10: Write failing tests for ChunkRepository**

```csharp
// backend/tests/MyMemo.Shared.Tests/Repositories/ChunkRepositoryTests.cs
using FluentAssertions;
using Microsoft.Data.Sqlite;
using MyMemo.Shared.Database;
using MyMemo.Shared.Repositories;

namespace MyMemo.Shared.Tests.Repositories;

public class ChunkRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ChunkRepository _sut;
    private readonly UserRepository _users;
    private readonly SessionRepository _sessions;

    public ChunkRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var factory = new InMemoryConnectionFactory(_connection);
        DatabaseInitializer.Initialize(factory).GetAwaiter().GetResult();
        _sut = new ChunkRepository(factory);
        _users = new UserRepository(factory);
        _sessions = new SessionRepository(factory);
    }

    private async Task<string> CreateTestSession()
    {
        var user = await _users.GetOrCreateByClerkIdAsync("clerk_test", "t@t.com", "T");
        var session = await _sessions.CreateAsync(user.Id, "full", "microphone");
        return session.Id;
    }

    [Fact]
    public async Task CreateAsync_ReturnsNewChunk()
    {
        var sessionId = await CreateTestSession();
        var chunk = await _sut.CreateAsync(sessionId, 0, "user/session/0.webm");

        chunk.Should().NotBeNull();
        chunk.SessionId.Should().Be(sessionId);
        chunk.ChunkIndex.Should().Be(0);
        chunk.Status.Should().Be("uploaded");
    }

    [Fact]
    public async Task ListBySessionAsync_ReturnsChunksInOrder()
    {
        var sessionId = await CreateTestSession();
        await _sut.CreateAsync(sessionId, 0, "path/0.webm");
        await _sut.CreateAsync(sessionId, 1, "path/1.webm");

        var chunks = await _sut.ListBySessionAsync(sessionId);

        chunks.Should().HaveCount(2);
        chunks[0].ChunkIndex.Should().Be(0);
        chunks[1].ChunkIndex.Should().Be(1);
    }

    [Fact]
    public async Task UpdateStatusAsync_ChangesStatus()
    {
        var sessionId = await CreateTestSession();
        var chunk = await _sut.CreateAsync(sessionId, 0, "path/0.webm");

        await _sut.UpdateStatusAsync(chunk.Id, "transcribed");

        var updated = await _sut.GetByIdAsync(chunk.Id);
        updated!.Status.Should().Be("transcribed");
    }

    [Fact]
    public async Task AreAllTranscribedAsync_ReturnsTrueWhenAllDone()
    {
        var sessionId = await CreateTestSession();
        var c1 = await _sut.CreateAsync(sessionId, 0, "path/0.webm");
        var c2 = await _sut.CreateAsync(sessionId, 1, "path/1.webm");

        await _sut.UpdateStatusAsync(c1.Id, "transcribed");
        await _sut.UpdateStatusAsync(c2.Id, "transcribed");

        var result = await _sut.AreAllTranscribedAsync(sessionId);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AreAllTranscribedAsync_ReturnsFalseWhenNotDone()
    {
        var sessionId = await CreateTestSession();
        await _sut.CreateAsync(sessionId, 0, "path/0.webm");
        var c2 = await _sut.CreateAsync(sessionId, 1, "path/1.webm");

        await _sut.UpdateStatusAsync(c2.Id, "transcribed");

        var result = await _sut.AreAllTranscribedAsync(sessionId);
        result.Should().BeFalse();
    }

    public void Dispose() => _connection.Dispose();
}
```

**Step 11: Run tests to verify they fail**

```bash
cd backend && dotnet test tests/MyMemo.Shared.Tests --filter "ChunkRepositoryTests" -v n
```

Expected: FAIL

**Step 12: Implement ChunkRepository**

```csharp
// backend/src/MyMemo.Shared/Repositories/IChunkRepository.cs
using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public interface IChunkRepository
{
    Task<Chunk> CreateAsync(string sessionId, int chunkIndex, string blobPath);
    Task<Chunk?> GetByIdAsync(string id);
    Task<IReadOnlyList<Chunk>> ListBySessionAsync(string sessionId);
    Task UpdateStatusAsync(string id, string status, string? errorMessage = null);
    Task<bool> AreAllTranscribedAsync(string sessionId);
}
```

```csharp
// backend/src/MyMemo.Shared/Repositories/ChunkRepository.cs
using Dapper;
using MyMemo.Shared.Database;
using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public sealed class ChunkRepository(IDbConnectionFactory db) : IChunkRepository
{
    public async Task<Chunk> CreateAsync(string sessionId, int chunkIndex, string blobPath)
    {
        using var conn = await db.CreateConnectionAsync();
        var id = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            """
            INSERT INTO chunks (id, session_id, chunk_index, blob_path, status, created_at, updated_at)
            VALUES (@id, @sessionId, @chunkIndex, @blobPath, 'uploaded', @now, @now)
            """,
            new { id, sessionId, chunkIndex, blobPath, now });

        return (await GetByIdAsync(id))!;
    }

    public async Task<Chunk?> GetByIdAsync(string id)
    {
        using var conn = await db.CreateConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<Chunk>(
            """
            SELECT id, session_id AS SessionId, chunk_index AS ChunkIndex, blob_path AS BlobPath,
                   duration_sec AS DurationSec, status, error_message AS ErrorMessage,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM chunks WHERE id = @id
            """,
            new { id });
    }

    public async Task<IReadOnlyList<Chunk>> ListBySessionAsync(string sessionId)
    {
        using var conn = await db.CreateConnectionAsync();
        var results = await conn.QueryAsync<Chunk>(
            """
            SELECT id, session_id AS SessionId, chunk_index AS ChunkIndex, blob_path AS BlobPath,
                   duration_sec AS DurationSec, status, error_message AS ErrorMessage,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM chunks WHERE session_id = @sessionId ORDER BY chunk_index
            """,
            new { sessionId });
        return results.ToList();
    }

    public async Task UpdateStatusAsync(string id, string status, string? errorMessage = null)
    {
        using var conn = await db.CreateConnectionAsync();
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            "UPDATE chunks SET status = @status, error_message = @errorMessage, updated_at = @now WHERE id = @id",
            new { id, status, errorMessage, now });
    }

    public async Task<bool> AreAllTranscribedAsync(string sessionId)
    {
        using var conn = await db.CreateConnectionAsync();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM chunks WHERE session_id = @sessionId AND status != 'transcribed'",
            new { sessionId });
        return count == 0;
    }
}
```

**Step 13: Run all repository tests**

```bash
cd backend && dotnet test tests/MyMemo.Shared.Tests -v n
```

Expected: ALL PASS

**Step 14: Implement TranscriptionRepository and MemoRepository**

```csharp
// backend/src/MyMemo.Shared/Repositories/ITranscriptionRepository.cs
using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public interface ITranscriptionRepository
{
    Task CreateAsync(string chunkId, string rawText, string language, double? confidence, string? wordTimestamps);
    Task<IReadOnlyList<Transcription>> ListBySessionAsync(string sessionId);
}
```

```csharp
// backend/src/MyMemo.Shared/Repositories/TranscriptionRepository.cs
using Dapper;
using MyMemo.Shared.Database;
using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public sealed class TranscriptionRepository(IDbConnectionFactory db) : ITranscriptionRepository
{
    public async Task CreateAsync(string chunkId, string rawText, string language, double? confidence, string? wordTimestamps)
    {
        using var conn = await db.CreateConnectionAsync();
        var id = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            """
            INSERT INTO transcriptions (id, chunk_id, raw_text, language, confidence, word_timestamps, created_at)
            VALUES (@id, @chunkId, @rawText, @language, @confidence, @wordTimestamps, @now)
            """,
            new { id, chunkId, rawText, language, confidence, wordTimestamps, now });
    }

    public async Task<IReadOnlyList<Transcription>> ListBySessionAsync(string sessionId)
    {
        using var conn = await db.CreateConnectionAsync();
        var results = await conn.QueryAsync<Transcription>(
            """
            SELECT t.id, t.chunk_id AS ChunkId, t.raw_text AS RawText, t.language, t.confidence,
                   t.word_timestamps AS WordTimestamps, t.created_at AS CreatedAt
            FROM transcriptions t
            INNER JOIN chunks c ON t.chunk_id = c.id
            WHERE c.session_id = @sessionId
            ORDER BY c.chunk_index
            """,
            new { sessionId });
        return results.ToList();
    }
}
```

```csharp
// backend/src/MyMemo.Shared/Repositories/IMemoRepository.cs
using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public interface IMemoRepository
{
    Task CreateAsync(string sessionId, string outputMode, string content, string modelUsed, int? promptTokens, int? completionTokens);
    Task<Memo?> GetBySessionIdAsync(string sessionId);
}
```

```csharp
// backend/src/MyMemo.Shared/Repositories/MemoRepository.cs
using Dapper;
using MyMemo.Shared.Database;
using MyMemo.Shared.Models;

namespace MyMemo.Shared.Repositories;

public sealed class MemoRepository(IDbConnectionFactory db) : IMemoRepository
{
    public async Task CreateAsync(string sessionId, string outputMode, string content, string modelUsed, int? promptTokens, int? completionTokens)
    {
        using var conn = await db.CreateConnectionAsync();
        var id = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        await conn.ExecuteAsync(
            """
            INSERT INTO memos (id, session_id, output_mode, content, model_used, prompt_tokens, completion_tokens, created_at)
            VALUES (@id, @sessionId, @outputMode, @content, @modelUsed, @promptTokens, @completionTokens, @now)
            """,
            new { id, sessionId, outputMode, content, modelUsed, promptTokens, completionTokens, now });
    }

    public async Task<Memo?> GetBySessionIdAsync(string sessionId)
    {
        using var conn = await db.CreateConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<Memo>(
            """
            SELECT id, session_id AS SessionId, output_mode AS OutputMode, content,
                   model_used AS ModelUsed, prompt_tokens AS PromptTokens,
                   completion_tokens AS CompletionTokens, created_at AS CreatedAt
            FROM memos WHERE session_id = @sessionId
            """,
            new { sessionId });
    }
}
```

**Step 15: Run full test suite**

```bash
cd backend && dotnet test -v n
```

Expected: ALL PASS

**Step 16: Commit**

```bash
git add backend/src/MyMemo.Shared/Database/ backend/src/MyMemo.Shared/Repositories/ backend/tests/MyMemo.Shared.Tests/
git commit -m "feat(backend): add database layer with connection factory and repositories"
```

---

## Task 4: Azure Service Wrappers

**Files:**
- Create: `backend/src/MyMemo.Shared/Services/IBlobStorageService.cs`
- Create: `backend/src/MyMemo.Shared/Services/BlobStorageService.cs`
- Create: `backend/src/MyMemo.Shared/Services/IQueueService.cs`
- Create: `backend/src/MyMemo.Shared/Services/QueueService.cs`
- Create: `backend/src/MyMemo.Shared/Services/IWhisperService.cs`
- Create: `backend/src/MyMemo.Shared/Services/WhisperService.cs`
- Create: `backend/src/MyMemo.Shared/Services/IMemoGeneratorService.cs`
- Create: `backend/src/MyMemo.Shared/Services/MemoGeneratorService.cs`
- Create: `backend/src/MyMemo.Shared/Services/ServiceConfiguration.cs`

**Step 1: Create configuration options**

```csharp
// backend/src/MyMemo.Shared/Services/ServiceConfiguration.cs
namespace MyMemo.Shared.Services;

public sealed class AzureBlobOptions
{
    public required string ConnectionString { get; init; }
    public string ContainerName { get; init; } = "audio-chunks";
}

public sealed class AzureServiceBusOptions
{
    public required string ConnectionString { get; init; }
    public string TranscriptionQueueName { get; init; } = "transcription-jobs";
    public string MemoGenerationQueueName { get; init; } = "memo-generation";
}

public sealed class AzureOpenAIOptions
{
    public required string Endpoint { get; init; }
    public required string ApiKey { get; init; }
    public string WhisperDeployment { get; init; } = "whisper-1";
    public string GptDeployment { get; init; } = "gpt-4.1-nano";
}
```

**Step 2: Implement BlobStorageService**

```csharp
// backend/src/MyMemo.Shared/Services/IBlobStorageService.cs
namespace MyMemo.Shared.Services;

public interface IBlobStorageService
{
    Task<string> UploadAsync(string blobPath, Stream content, string contentType);
    Task<Stream> DownloadAsync(string blobPath);
}
```

```csharp
// backend/src/MyMemo.Shared/Services/BlobStorageService.cs
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace MyMemo.Shared.Services;

public sealed class BlobStorageService(IOptions<AzureBlobOptions> options) : IBlobStorageService
{
    private readonly BlobContainerClient _container = new(options.Value.ConnectionString, options.Value.ContainerName);

    public async Task<string> UploadAsync(string blobPath, Stream content, string contentType)
    {
        var blob = _container.GetBlobClient(blobPath);
        await blob.UploadAsync(content, overwrite: true);
        return blobPath;
    }

    public async Task<Stream> DownloadAsync(string blobPath)
    {
        var blob = _container.GetBlobClient(blobPath);
        var response = await blob.DownloadStreamingAsync();
        return response.Value.Content;
    }
}
```

**Step 3: Implement QueueService**

```csharp
// backend/src/MyMemo.Shared/Services/IQueueService.cs
namespace MyMemo.Shared.Services;

public interface IQueueService
{
    Task SendTranscriptionJobAsync(string sessionId, string chunkId, int chunkIndex, string blobPath, string language = "da");
    Task SendMemoGenerationJobAsync(string sessionId);
}
```

```csharp
// backend/src/MyMemo.Shared/Services/QueueService.cs
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace MyMemo.Shared.Services;

public sealed class QueueService : IQueueService, IAsyncDisposable
{
    private readonly ServiceBusSender _transcriptionSender;
    private readonly ServiceBusSender _memoSender;
    private readonly ServiceBusClient _client;

    public QueueService(IOptions<AzureServiceBusOptions> options)
    {
        _client = new ServiceBusClient(options.Value.ConnectionString);
        _transcriptionSender = _client.CreateSender(options.Value.TranscriptionQueueName);
        _memoSender = _client.CreateSender(options.Value.MemoGenerationQueueName);
    }

    public async Task SendTranscriptionJobAsync(string sessionId, string chunkId, int chunkIndex, string blobPath, string language = "da")
    {
        var body = JsonSerializer.Serialize(new { sessionId, chunkId, chunkIndex, blobPath, language });
        await _transcriptionSender.SendMessageAsync(new ServiceBusMessage(body));
    }

    public async Task SendMemoGenerationJobAsync(string sessionId)
    {
        var body = JsonSerializer.Serialize(new { sessionId });
        await _memoSender.SendMessageAsync(new ServiceBusMessage(body));
    }

    public async ValueTask DisposeAsync()
    {
        await _transcriptionSender.DisposeAsync();
        await _memoSender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
```

**Step 4: Implement WhisperService**

```csharp
// backend/src/MyMemo.Shared/Services/IWhisperService.cs
namespace MyMemo.Shared.Services;

public sealed record WhisperResult(string Text, double? AverageConfidence, string? WordTimestampsJson);

public interface IWhisperService
{
    Task<WhisperResult> TranscribeAsync(Stream audioStream, string language = "da");
}
```

```csharp
// backend/src/MyMemo.Shared/Services/WhisperService.cs
using System.ClientModel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Audio;

namespace MyMemo.Shared.Services;

public sealed class WhisperService(IOptions<AzureOpenAIOptions> options) : IWhisperService
{
    public async Task<WhisperResult> TranscribeAsync(Stream audioStream, string language = "da")
    {
        var credential = new ApiKeyCredential(options.Value.ApiKey);
        var client = new AzureOpenAIClient(new Uri(options.Value.Endpoint), credential);
        var audioClient = client.GetAudioClient(options.Value.WhisperDeployment);

        var transcriptionOptions = new AudioTranscriptionOptions
        {
            Language = language,
            ResponseFormat = AudioTranscriptionFormat.Verbose,
            TimestampGranularities = AudioTimestampGranularity.Word
        };

        var result = await audioClient.TranscribeAudioAsync(audioStream, "audio.webm", transcriptionOptions);
        var transcription = result.Value;

        string? wordTimestamps = null;
        if (transcription.Words?.Count > 0)
        {
            var words = transcription.Words.Select(w => new { word = w.Word, start = w.StartTime.TotalSeconds, end = w.EndTime.TotalSeconds });
            wordTimestamps = JsonSerializer.Serialize(words);
        }

        return new WhisperResult(transcription.Text, null, wordTimestamps);
    }
}
```

**Step 5: Implement MemoGeneratorService**

```csharp
// backend/src/MyMemo.Shared/Services/IMemoGeneratorService.cs
namespace MyMemo.Shared.Services;

public sealed record MemoResult(string Content, string ModelUsed, int PromptTokens, int CompletionTokens);

public interface IMemoGeneratorService
{
    Task<MemoResult> GenerateAsync(string fullTranscription, string outputMode);
}
```

```csharp
// backend/src/MyMemo.Shared/Services/MemoGeneratorService.cs
using System.ClientModel;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace MyMemo.Shared.Services;

public sealed class MemoGeneratorService(IOptions<AzureOpenAIOptions> options) : IMemoGeneratorService
{
    private const string FullModePrompt = """
        Du er en professionel dansk transskribent. Renskiv følgende rå transkription.

        Regler:
        - Ret grammatik og stavefejl
        - Tilføj korrekt tegnsætning
        - Bevar talerens oprindelige ordvalg og tone
        - Strukturer i afsnit med logiske pauser
        - Marker tydeligt hvis noget er uhørbart: [uhørbart]
        - Bevar tidsstempler som sektion-markører
        - Output på dansk
        """;

    private const string SummaryModePrompt = """
        Du er en professionel dansk mødesekretær. Lav et struktureret referat af følgende transkription.

        Format:
        - Titel/emne (udledt fra indhold)
        - Dato og varighed
        - Deltagere (hvis nævnt)
        - Hovedpunkter (kort, præcist)
        - Beslutninger
        - Action items (hvem, hvad, hvornår)
        - Næste skridt

        Regler:
        - Skriv på dansk
        - Vær koncis men præcis
        - Brug ikke mere end 1 side til referatet
        - Marker usikre punkter med [?]
        """;

    public async Task<MemoResult> GenerateAsync(string fullTranscription, string outputMode)
    {
        var credential = new ApiKeyCredential(options.Value.ApiKey);
        var client = new AzureOpenAIClient(new Uri(options.Value.Endpoint), credential);
        var chatClient = client.GetChatClient(options.Value.GptDeployment);

        var systemPrompt = outputMode == "summary" ? SummaryModePrompt : FullModePrompt;

        var result = await chatClient.CompleteChatAsync(
        [
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(fullTranscription)
        ]);

        var completion = result.Value;

        return new MemoResult(
            Content: completion.Content[0].Text,
            ModelUsed: options.Value.GptDeployment,
            PromptTokens: completion.Usage.InputTokenCount,
            CompletionTokens: completion.Usage.OutputTokenCount);
    }
}
```

**Step 6: Verify build**

```bash
cd backend && dotnet build
```

Expected: BUILD SUCCEEDED

**Step 7: Commit**

```bash
git add backend/src/MyMemo.Shared/Services/
git commit -m "feat(backend): add Azure service wrappers for Blob, Service Bus, and OpenAI"
```

---

## Task 5: Clerk Auth Middleware

**Files:**
- Create: `backend/src/MyMemo.Api/Auth/ClerkAuthExtensions.cs`
- Test: `backend/tests/MyMemo.Api.Tests/Auth/ClerkAuthTests.cs`

**Step 1: Write failing test**

```csharp
// backend/tests/MyMemo.Api.Tests/Auth/ClerkAuthTests.cs
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MyMemo.Api.Tests.Auth;

public class ClerkAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ClerkAuthTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_WithoutToken()
    {
        var response = await _client.GetAsync("/api/sessions");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HealthEndpoint_Returns200_WithoutToken()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
cd backend && dotnet test tests/MyMemo.Api.Tests --filter "ClerkAuthTests" -v n
```

Expected: FAIL — /api/sessions endpoint doesn't exist yet (returns 404, not 401).

**Step 3: Implement Clerk auth extension**

```csharp
// backend/src/MyMemo.Api/Auth/ClerkAuthExtensions.cs
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace MyMemo.Api.Auth;

public static class ClerkAuthExtensions
{
    public static IServiceCollection AddClerkAuth(this IServiceCollection services, IConfiguration config)
    {
        var clerkDomain = config["Clerk:Domain"]
            ?? throw new InvalidOperationException("Clerk:Domain not configured");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"https://{clerkDomain}";
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = $"https://{clerkDomain}",
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    NameClaimType = "sub"
                };
            });

        services.AddAuthorization();

        return services;
    }
}
```

**Step 4: Update Program.cs with auth + a stub sessions endpoint**

```csharp
// backend/src/MyMemo.Api/Program.cs
using MyMemo.Api.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddClerkAuth(builder.Configuration);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/api/sessions", () => Results.Ok(Array.Empty<object>()))
    .RequireAuthorization();

app.Run();

public partial class Program { }
```

**Step 5: Add Clerk config to appsettings.Development.json**

```json
// backend/src/MyMemo.Api/appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Clerk": {
    "Domain": "YOUR_CLERK_DOMAIN.clerk.accounts.dev"
  }
}
```

**Step 6: Run tests**

```bash
cd backend && dotnet test tests/MyMemo.Api.Tests --filter "ClerkAuthTests" -v n
```

Expected: `ProtectedEndpoint_Returns401_WithoutToken` PASSES, `HealthEndpoint_Returns200_WithoutToken` PASSES. Note: the Clerk JWKS fetch may fail in test environment — if so, override `WebApplicationFactory` to use a test auth handler.

If auth test is flaky due to external JWKS, add a test auth handler:

```csharp
// Modify the test to use a custom WebApplicationFactory
// backend/tests/MyMemo.Api.Tests/Auth/ClerkAuthTests.cs
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MyMemo.Api.Tests.Auth;

public class ClerkAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ClerkAuthTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        });
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_WithoutToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/sessions");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HealthEndpoint_Returns200_WithoutToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

```csharp
// backend/tests/MyMemo.Api.Tests/Auth/TestAuthHandler.cs
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MyMemo.Api.Tests.Auth;

public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "test-clerk-id"), new Claim("sub", "test-clerk-id") };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

**Step 7: Run tests again**

```bash
cd backend && dotnet test tests/MyMemo.Api.Tests --filter "ClerkAuthTests" -v n
```

Expected: PASS (2 tests)

**Step 8: Commit**

```bash
git add backend/src/MyMemo.Api/ backend/tests/MyMemo.Api.Tests/
git commit -m "feat(backend): add Clerk JWT authentication middleware"
```

---

## Task 6: Session CRUD Endpoints

**Files:**
- Create: `backend/src/MyMemo.Api/Endpoints/SessionEndpoints.cs`
- Create: `backend/src/MyMemo.Api/Endpoints/Requests.cs`
- Test: `backend/tests/MyMemo.Api.Tests/Endpoints/SessionEndpointsTests.cs`

**Step 1: Write failing tests**

```csharp
// backend/tests/MyMemo.Api.Tests/Endpoints/SessionEndpointsTests.cs
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MyMemo.Api.Tests.Auth;
using MyMemo.Shared.Database;

namespace MyMemo.Api.Tests.Endpoints;

public class SessionEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SessionEndpointsTests(WebApplicationFactory<Program> factory)
    {
        var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

                // Use in-memory SQLite for tests
                services.AddSingleton<IDbConnectionFactory>(_ =>
                {
                    var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
                    conn.Open();
                    var factory = new TestConnectionFactory(conn);
                    DatabaseInitializer.Initialize(factory).GetAwaiter().GetResult();
                    return factory;
                });
            });
        });

        _client = customFactory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "test-token");
    }

    [Fact]
    public async Task CreateSession_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<SessionResponse>();
        body!.Status.Should().Be("recording");
        body.OutputMode.Should().Be("full");
    }

    [Fact]
    public async Task ListSessions_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/api/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSession_Returns404_WhenNotFound()
    {
        var response = await _client.GetAsync("/api/sessions/nonexistent");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteSession_Returns204()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone" });
        var session = await createResponse.Content.ReadFromJsonAsync<SessionResponse>();

        var response = await _client.DeleteAsync($"/api/sessions/{session!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private sealed record SessionResponse(string Id, string Status, string OutputMode);
}

internal class TestConnectionFactory(Microsoft.Data.Sqlite.SqliteConnection connection) : IDbConnectionFactory
{
    public Task<Microsoft.Data.Sqlite.SqliteConnection> CreateConnectionAsync() => Task.FromResult(connection);
}
```

**Step 2: Run test to verify it fails**

```bash
cd backend && dotnet test tests/MyMemo.Api.Tests --filter "SessionEndpointsTests" -v n
```

Expected: FAIL — endpoints not implemented.

**Step 3: Create request/response DTOs**

```csharp
// backend/src/MyMemo.Api/Endpoints/Requests.cs
namespace MyMemo.Api.Endpoints;

public sealed record CreateSessionRequest(string OutputMode = "full", string AudioSource = "microphone");
```

**Step 4: Implement SessionEndpoints**

```csharp
// backend/src/MyMemo.Api/Endpoints/SessionEndpoints.cs
using System.Security.Claims;
using MyMemo.Shared.Repositories;

namespace MyMemo.Api.Endpoints;

public static class SessionEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions").RequireAuthorization();

        group.MapPost("", CreateSession);
        group.MapGet("", ListSessions);
        group.MapGet("{id}", GetSession);
        group.MapDelete("{id}", DeleteSession);
    }

    private static async Task<IResult> CreateSession(
        CreateSessionRequest request,
        ISessionRepository sessions,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub")
            ?? return Results.Unauthorized();
        var email = principal.FindFirstValue(ClaimTypes.Email) ?? "";
        var name = principal.FindFirstValue(ClaimTypes.Name) ?? "";
        var user = await users.GetOrCreateByClerkIdAsync(clerkId, email, name);

        var session = await sessions.CreateAsync(user.Id, request.OutputMode, request.AudioSource);
        return Results.Created($"/api/sessions/{session.Id}", session);
    }

    private static async Task<IResult> ListSessions(
        ISessionRepository sessions,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub")
            ?? return Results.Unauthorized();
        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");

        var list = await sessions.ListByUserAsync(user.Id);
        return Results.Ok(list);
    }

    private static async Task<IResult> GetSession(
        string id,
        ISessionRepository sessions,
        IChunkRepository chunks,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub")
            ?? return Results.Unauthorized();
        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");

        var session = await sessions.GetByIdAsync(id);
        if (session is null || session.UserId != user.Id)
            return Results.NotFound();

        var sessionChunks = await chunks.ListBySessionAsync(id);
        return Results.Ok(new { session, chunks = sessionChunks });
    }

    private static async Task<IResult> DeleteSession(
        string id,
        ISessionRepository sessions,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub")
            ?? return Results.Unauthorized();
        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");

        var session = await sessions.GetByIdAsync(id);
        if (session is null || session.UserId != user.Id)
            return Results.NotFound();

        await sessions.DeleteAsync(id);
        return Results.NoContent();
    }
}
```

Note: The `?? return` pattern doesn't compile in C#. Fix by using early returns:

```csharp
    private static async Task<IResult> CreateSession(
        CreateSessionRequest request,
        ISessionRepository sessions,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        var email = principal.FindFirstValue(ClaimTypes.Email) ?? "";
        var name = principal.FindFirstValue(ClaimTypes.Name) ?? "";
        var user = await users.GetOrCreateByClerkIdAsync(clerkId, email, name);

        var session = await sessions.CreateAsync(user.Id, request.OutputMode, request.AudioSource);
        return Results.Created($"/api/sessions/{session.Id}", session);
    }
```

Apply the same pattern to all endpoint methods (replace `?? return` with `if ... return`).

**Step 5: Wire up DI in Program.cs**

```csharp
// backend/src/MyMemo.Api/Program.cs
using MyMemo.Api.Auth;
using MyMemo.Api.Endpoints;
using MyMemo.Shared.Database;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddClerkAuth(builder.Configuration);

// Database
var dbConnectionString = builder.Configuration.GetConnectionString("Database")
    ?? "Data Source=mymemo.db";
builder.Services.AddSingleton<IDbConnectionFactory>(new SqliteConnectionFactory(dbConnectionString));

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<IChunkRepository, ChunkRepository>();
builder.Services.AddScoped<ITranscriptionRepository, TranscriptionRepository>();
builder.Services.AddScoped<IMemoRepository, MemoRepository>();

// Azure services
builder.Services.Configure<AzureBlobOptions>(builder.Configuration.GetSection("AzureBlob"));
builder.Services.Configure<AzureServiceBusOptions>(builder.Configuration.GetSection("AzureServiceBus"));
builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection("AzureOpenAI"));
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
builder.Services.AddSingleton<IQueueService, QueueService>();

var app = builder.Build();

// Initialize database
var dbFactory = app.Services.GetRequiredService<IDbConnectionFactory>();
await DatabaseInitializer.Initialize(dbFactory);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
SessionEndpoints.Map(app);

app.Run();

public partial class Program { }
```

**Step 6: Run tests**

```bash
cd backend && dotnet test tests/MyMemo.Api.Tests --filter "SessionEndpointsTests" -v n
```

Expected: PASS (4 tests)

**Step 7: Commit**

```bash
git add backend/src/MyMemo.Api/ backend/tests/MyMemo.Api.Tests/
git commit -m "feat(backend): add session CRUD endpoints with auth"
```

---

## Task 7: Chunk Upload Endpoint

**Files:**
- Create: `backend/src/MyMemo.Api/Endpoints/ChunkEndpoints.cs`
- Test: `backend/tests/MyMemo.Api.Tests/Endpoints/ChunkEndpointsTests.cs`

**Step 1: Write failing test**

```csharp
// backend/tests/MyMemo.Api.Tests/Endpoints/ChunkEndpointsTests.cs
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MyMemo.Api.Tests.Auth;
using MyMemo.Shared.Database;
using MyMemo.Shared.Services;
using NSubstitute;

namespace MyMemo.Api.Tests.Endpoints;

public class ChunkEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly IBlobStorageService _blobService;
    private readonly IQueueService _queueService;

    public ChunkEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _blobService = Substitute.For<IBlobStorageService>();
        _queueService = Substitute.For<IQueueService>();

        var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                services.AddSingleton<IDbConnectionFactory>(_ =>
                {
                    var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
                    conn.Open();
                    var f = new TestConnectionFactory(conn);
                    DatabaseInitializer.Initialize(f).GetAwaiter().GetResult();
                    return f;
                });
                services.AddSingleton(_blobService);
                services.AddSingleton(_queueService);
            });
        });

        _client = customFactory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "test-token");
    }

    [Fact]
    public async Task UploadChunk_Returns202()
    {
        // Create a session first
        var sessionResponse = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone" });
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionIdResponse>();

        _blobService.UploadAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>())
            .Returns("path/0.webm");

        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "audio", "chunk.webm");
        content.Add(new StringContent("0"), "chunkIndex");

        var response = await _client.PostAsync($"/api/sessions/{session!.Id}/chunks", content);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    private sealed record SessionIdResponse(string Id);
}
```

**Step 2: Run test to verify it fails**

```bash
cd backend && dotnet test tests/MyMemo.Api.Tests --filter "ChunkEndpointsTests" -v n
```

Expected: FAIL

**Step 3: Implement ChunkEndpoints**

```csharp
// backend/src/MyMemo.Api/Endpoints/ChunkEndpoints.cs
using System.Security.Claims;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;

namespace MyMemo.Api.Endpoints;

public static class ChunkEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sessions/{sessionId}/chunks", UploadChunk)
            .RequireAuthorization()
            .DisableAntiforgery();
    }

    private static async Task<IResult> UploadChunk(
        string sessionId,
        IFormFile audio,
        int chunkIndex,
        ISessionRepository sessions,
        IChunkRepository chunks,
        IUserRepository users,
        IBlobStorageService blobService,
        IQueueService queueService,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");
        var session = await sessions.GetByIdAsync(sessionId);
        if (session is null || session.UserId != user.Id)
            return Results.NotFound();

        var blobPath = $"{user.Id}/{sessionId}/{chunkIndex}.webm";
        await using var stream = audio.OpenReadStream();
        await blobService.UploadAsync(blobPath, stream, audio.ContentType);

        var chunk = await chunks.CreateAsync(sessionId, chunkIndex, blobPath);
        await chunks.UpdateStatusAsync(chunk.Id, "queued");
        await queueService.SendTranscriptionJobAsync(sessionId, chunk.Id, chunkIndex, blobPath);

        return Results.Accepted($"/api/sessions/{sessionId}", chunk);
    }
}
```

**Step 4: Register ChunkEndpoints in Program.cs**

Add after `SessionEndpoints.Map(app);`:
```csharp
ChunkEndpoints.Map(app);
```

**Step 5: Run tests**

```bash
cd backend && dotnet test tests/MyMemo.Api.Tests --filter "ChunkEndpointsTests" -v n
```

Expected: PASS

**Step 6: Commit**

```bash
git add backend/src/MyMemo.Api/ backend/tests/MyMemo.Api.Tests/
git commit -m "feat(backend): add chunk upload endpoint with blob storage and queue"
```

---

## Task 8: Finalize & Memo Endpoints

**Files:**
- Create: `backend/src/MyMemo.Api/Endpoints/MemoEndpoints.cs`
- Test: `backend/tests/MyMemo.Api.Tests/Endpoints/MemoEndpointsTests.cs`

**Step 1: Write failing tests**

```csharp
// backend/tests/MyMemo.Api.Tests/Endpoints/MemoEndpointsTests.cs
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MyMemo.Api.Tests.Auth;
using MyMemo.Shared.Database;
using MyMemo.Shared.Services;
using NSubstitute;

namespace MyMemo.Api.Tests.Endpoints;

public class MemoEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly IQueueService _queueService;

    public MemoEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _queueService = Substitute.For<IQueueService>();

        var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                services.AddSingleton<IDbConnectionFactory>(_ =>
                {
                    var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
                    conn.Open();
                    var f = new TestConnectionFactory(conn);
                    DatabaseInitializer.Initialize(f).GetAwaiter().GetResult();
                    return f;
                });
                services.AddSingleton(Substitute.For<IBlobStorageService>());
                services.AddSingleton(_queueService);
            });
        });

        _client = customFactory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "test-token");
    }

    [Fact]
    public async Task FinalizeSession_Returns202()
    {
        var sessionResponse = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone" });
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionIdResponse>();

        var response = await _client.PostAsync($"/api/sessions/{session!.Id}/finalize", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task GetMemo_Returns404_WhenNoMemo()
    {
        var sessionResponse = await _client.PostAsJsonAsync("/api/sessions", new { outputMode = "full", audioSource = "microphone" });
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionIdResponse>();

        var response = await _client.GetAsync($"/api/sessions/{session!.Id}/memo");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record SessionIdResponse(string Id);
}
```

**Step 2: Run tests to verify they fail**

```bash
cd backend && dotnet test tests/MyMemo.Api.Tests --filter "MemoEndpointsTests" -v n
```

Expected: FAIL

**Step 3: Implement MemoEndpoints**

```csharp
// backend/src/MyMemo.Api/Endpoints/MemoEndpoints.cs
using System.Security.Claims;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;

namespace MyMemo.Api.Endpoints;

public static class MemoEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sessions/{sessionId}/finalize", FinalizeSession)
            .RequireAuthorization();
        app.MapGet("/api/sessions/{sessionId}/memo", GetMemo)
            .RequireAuthorization();
    }

    private static async Task<IResult> FinalizeSession(
        string sessionId,
        ISessionRepository sessions,
        IUserRepository users,
        IQueueService queueService,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");
        var session = await sessions.GetByIdAsync(sessionId);
        if (session is null || session.UserId != user.Id)
            return Results.NotFound();

        await sessions.SetEndedAtAsync(sessionId);
        await sessions.UpdateStatusAsync(sessionId, "processing");
        await queueService.SendMemoGenerationJobAsync(sessionId);

        return Results.Accepted($"/api/sessions/{sessionId}/memo");
    }

    private static async Task<IResult> GetMemo(
        string sessionId,
        ISessionRepository sessions,
        IMemoRepository memos,
        IUserRepository users,
        ClaimsPrincipal principal)
    {
        var clerkId = principal.FindFirstValue("sub");
        if (clerkId is null) return Results.Unauthorized();

        var user = await users.GetOrCreateByClerkIdAsync(clerkId, "", "");
        var session = await sessions.GetByIdAsync(sessionId);
        if (session is null || session.UserId != user.Id)
            return Results.NotFound();

        var memo = await memos.GetBySessionIdAsync(sessionId);
        if (memo is null) return Results.NotFound();

        return Results.Ok(memo);
    }
}
```

**Step 4: Register MemoEndpoints in Program.cs**

Add after `ChunkEndpoints.Map(app);`:
```csharp
MemoEndpoints.Map(app);
```

**Step 5: Run tests**

```bash
cd backend && dotnet test tests/MyMemo.Api.Tests --filter "MemoEndpointsTests" -v n
```

Expected: PASS

**Step 6: Commit**

```bash
git add backend/src/MyMemo.Api/ backend/tests/MyMemo.Api.Tests/
git commit -m "feat(backend): add finalize and memo endpoints"
```

---

## Task 9: Worker Service — Transcription Processing

**Files:**
- Create: `backend/src/MyMemo.Worker/Workers/TranscriptionWorker.cs`
- Test: `backend/tests/MyMemo.Worker.Tests/Workers/TranscriptionWorkerTests.cs`

**Step 1: Write failing test**

```csharp
// backend/tests/MyMemo.Worker.Tests/Workers/TranscriptionWorkerTests.cs
using FluentAssertions;
using NSubstitute;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;
using MyMemo.Worker.Workers;

namespace MyMemo.Worker.Tests.Workers;

public class TranscriptionWorkerTests
{
    private readonly IChunkRepository _chunks = Substitute.For<IChunkRepository>();
    private readonly ITranscriptionRepository _transcriptions = Substitute.For<ITranscriptionRepository>();
    private readonly IBlobStorageService _blobService = Substitute.For<IBlobStorageService>();
    private readonly IWhisperService _whisperService = Substitute.For<IWhisperService>();
    private readonly IQueueService _queueService = Substitute.For<IQueueService>();
    private readonly TranscriptionProcessor _sut;

    public TranscriptionWorkerTests()
    {
        _sut = new TranscriptionProcessor(_chunks, _transcriptions, _blobService, _whisperService, _queueService);
    }

    [Fact]
    public async Task ProcessAsync_TranscribesChunkAndStoresResult()
    {
        var audioStream = new MemoryStream(new byte[] { 1, 2, 3 });
        _blobService.DownloadAsync("path/0.webm").Returns(audioStream);
        _whisperService.TranscribeAsync(audioStream, "da")
            .Returns(new WhisperResult("Hej med dig", 0.95, null));
        _chunks.AreAllTranscribedAsync("session-1").Returns(false);

        await _sut.ProcessAsync("session-1", "chunk-1", 0, "path/0.webm", "da");

        await _chunks.Received(1).UpdateStatusAsync("chunk-1", "transcribing");
        await _transcriptions.Received(1).CreateAsync("chunk-1", "Hej med dig", "da", 0.95, null);
        await _chunks.Received(1).UpdateStatusAsync("chunk-1", "transcribed");
    }

    [Fact]
    public async Task ProcessAsync_EnqueuesMemoGeneration_WhenAllChunksTranscribed()
    {
        var audioStream = new MemoryStream(new byte[] { 1, 2, 3 });
        _blobService.DownloadAsync("path/0.webm").Returns(audioStream);
        _whisperService.TranscribeAsync(audioStream, "da")
            .Returns(new WhisperResult("Hej", null, null));
        _chunks.AreAllTranscribedAsync("session-1").Returns(true);

        await _sut.ProcessAsync("session-1", "chunk-1", 0, "path/0.webm", "da");

        await _queueService.Received(1).SendMemoGenerationJobAsync("session-1");
    }

    [Fact]
    public async Task ProcessAsync_SetsFailedStatus_OnError()
    {
        _blobService.DownloadAsync("path/0.webm").Returns<Stream>(_ => throw new Exception("Blob not found"));

        await _sut.ProcessAsync("session-1", "chunk-1", 0, "path/0.webm", "da");

        await _chunks.Received(1).UpdateStatusAsync("chunk-1", "failed", "Blob not found");
    }
}
```

**Step 2: Run test to verify it fails**

```bash
cd backend && dotnet test tests/MyMemo.Worker.Tests --filter "TranscriptionWorkerTests" -v n
```

Expected: FAIL — TranscriptionProcessor doesn't exist.

**Step 3: Implement TranscriptionProcessor (core logic)**

```csharp
// backend/src/MyMemo.Worker/Workers/TranscriptionWorker.cs
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;

namespace MyMemo.Worker.Workers;

public sealed class TranscriptionProcessor(
    IChunkRepository chunks,
    ITranscriptionRepository transcriptions,
    IBlobStorageService blobService,
    IWhisperService whisperService,
    IQueueService queueService)
{
    public async Task ProcessAsync(string sessionId, string chunkId, int chunkIndex, string blobPath, string language)
    {
        try
        {
            await chunks.UpdateStatusAsync(chunkId, "transcribing");

            await using var audioStream = await blobService.DownloadAsync(blobPath);
            var result = await whisperService.TranscribeAsync(audioStream, language);

            await transcriptions.CreateAsync(chunkId, result.Text, language, result.AverageConfidence, result.WordTimestampsJson);
            await chunks.UpdateStatusAsync(chunkId, "transcribed");

            if (await chunks.AreAllTranscribedAsync(sessionId))
            {
                await queueService.SendMemoGenerationJobAsync(sessionId);
            }
        }
        catch (Exception ex)
        {
            await chunks.UpdateStatusAsync(chunkId, "failed", ex.Message);
        }
    }
}

public sealed class TranscriptionWorker(
    IServiceProvider serviceProvider,
    IOptions<AzureServiceBusOptions> options,
    ILogger<TranscriptionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = new ServiceBusClient(options.Value.ConnectionString);
        var processor = client.CreateProcessor(options.Value.TranscriptionQueueName);

        processor.ProcessMessageAsync += async args =>
        {
            var body = JsonSerializer.Deserialize<TranscriptionJob>(args.Message.Body.ToString())!;
            logger.LogInformation("Processing transcription job for session {SessionId}, chunk {ChunkIndex}", body.SessionId, body.ChunkIndex);

            using var scope = serviceProvider.CreateScope();
            var transcriptionProcessor = new TranscriptionProcessor(
                scope.ServiceProvider.GetRequiredService<IChunkRepository>(),
                scope.ServiceProvider.GetRequiredService<ITranscriptionRepository>(),
                scope.ServiceProvider.GetRequiredService<IBlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<IWhisperService>(),
                scope.ServiceProvider.GetRequiredService<IQueueService>());

            await transcriptionProcessor.ProcessAsync(body.SessionId, body.ChunkId, body.ChunkIndex, body.BlobPath, body.Language);
            await args.CompleteMessageAsync(args.Message);
        };

        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Service Bus processing error");
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }

        await processor.StopProcessingAsync();
    }

    private sealed record TranscriptionJob(string SessionId, string ChunkId, int ChunkIndex, string BlobPath, string Language);
}
```

**Step 4: Run tests**

```bash
cd backend && dotnet test tests/MyMemo.Worker.Tests --filter "TranscriptionWorkerTests" -v n
```

Expected: PASS (3 tests)

**Step 5: Commit**

```bash
git add backend/src/MyMemo.Worker/ backend/tests/MyMemo.Worker.Tests/
git commit -m "feat(backend): add transcription worker with Whisper integration"
```

---

## Task 10: Worker Service — Memo Generation

**Files:**
- Create: `backend/src/MyMemo.Worker/Workers/MemoGenerationWorker.cs`
- Test: `backend/tests/MyMemo.Worker.Tests/Workers/MemoGenerationWorkerTests.cs`

**Step 1: Write failing test**

```csharp
// backend/tests/MyMemo.Worker.Tests/Workers/MemoGenerationWorkerTests.cs
using FluentAssertions;
using NSubstitute;
using MyMemo.Shared.Models;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;
using MyMemo.Worker.Workers;

namespace MyMemo.Worker.Tests.Workers;

public class MemoGenerationWorkerTests
{
    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly ITranscriptionRepository _transcriptions = Substitute.For<ITranscriptionRepository>();
    private readonly IMemoRepository _memos = Substitute.For<IMemoRepository>();
    private readonly IMemoGeneratorService _memoGenerator = Substitute.For<IMemoGeneratorService>();
    private readonly MemoGenerationProcessor _sut;

    public MemoGenerationWorkerTests()
    {
        _sut = new MemoGenerationProcessor(_sessions, _transcriptions, _memos, _memoGenerator);
    }

    [Fact]
    public async Task ProcessAsync_GeneratesMemoAndStores()
    {
        _sessions.GetByIdAsync("session-1").Returns(new Session
        {
            Id = "session-1", UserId = "user-1", Status = "processing",
            OutputMode = "full", AudioSource = "microphone",
            StartedAt = "2026-01-01 00:00:00", CreatedAt = "2026-01-01 00:00:00", UpdatedAt = "2026-01-01 00:00:00"
        });

        _transcriptions.ListBySessionAsync("session-1").Returns(new List<Transcription>
        {
            new() { Id = "t1", ChunkId = "c1", RawText = "Hej med dig.", CreatedAt = "2026-01-01 00:00:00" },
            new() { Id = "t2", ChunkId = "c2", RawText = "Hvordan går det?", CreatedAt = "2026-01-01 00:05:00" }
        });

        _memoGenerator.GenerateAsync("Hej med dig.\n\nHvordan går det?", "full")
            .Returns(new MemoResult("Renskrevet memo", "gpt-4.1-nano", 100, 50));

        await _sut.ProcessAsync("session-1");

        await _memos.Received(1).CreateAsync("session-1", "full", "Renskrevet memo", "gpt-4.1-nano", 100, 50);
        await _sessions.Received(1).UpdateStatusAsync("session-1", "completed");
    }

    [Fact]
    public async Task ProcessAsync_SetsFailedStatus_OnError()
    {
        _sessions.GetByIdAsync("session-1").Returns(new Session
        {
            Id = "session-1", UserId = "user-1", Status = "processing",
            OutputMode = "full", AudioSource = "microphone",
            StartedAt = "2026-01-01 00:00:00", CreatedAt = "2026-01-01 00:00:00", UpdatedAt = "2026-01-01 00:00:00"
        });

        _transcriptions.ListBySessionAsync("session-1").Returns(new List<Transcription>());
        _memoGenerator.GenerateAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns<MemoResult>(_ => throw new Exception("LLM error"));

        await _sut.ProcessAsync("session-1");

        await _sessions.Received(1).UpdateStatusAsync("session-1", "failed");
    }
}
```

**Step 2: Run test to verify it fails**

```bash
cd backend && dotnet test tests/MyMemo.Worker.Tests --filter "MemoGenerationWorkerTests" -v n
```

Expected: FAIL

**Step 3: Implement MemoGenerationProcessor**

```csharp
// backend/src/MyMemo.Worker/Workers/MemoGenerationWorker.cs
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;

namespace MyMemo.Worker.Workers;

public sealed class MemoGenerationProcessor(
    ISessionRepository sessions,
    ITranscriptionRepository transcriptions,
    IMemoRepository memos,
    IMemoGeneratorService memoGenerator)
{
    public async Task ProcessAsync(string sessionId)
    {
        try
        {
            var session = await sessions.GetByIdAsync(sessionId);
            if (session is null) return;

            var allTranscriptions = await transcriptions.ListBySessionAsync(sessionId);
            var fullText = string.Join("\n\n", allTranscriptions.Select(t => t.RawText));

            var result = await memoGenerator.GenerateAsync(fullText, session.OutputMode);

            await memos.CreateAsync(sessionId, session.OutputMode, result.Content, result.ModelUsed, result.PromptTokens, result.CompletionTokens);
            await sessions.UpdateStatusAsync(sessionId, "completed");
        }
        catch (Exception)
        {
            await sessions.UpdateStatusAsync(sessionId, "failed");
        }
    }
}

public sealed class MemoGenerationWorker(
    IServiceProvider serviceProvider,
    IOptions<AzureServiceBusOptions> options,
    ILogger<MemoGenerationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = new ServiceBusClient(options.Value.ConnectionString);
        var processor = client.CreateProcessor(options.Value.MemoGenerationQueueName);

        processor.ProcessMessageAsync += async args =>
        {
            var body = JsonSerializer.Deserialize<MemoJob>(args.Message.Body.ToString())!;
            logger.LogInformation("Processing memo generation for session {SessionId}", body.SessionId);

            using var scope = serviceProvider.CreateScope();
            var memoProcessor = new MemoGenerationProcessor(
                scope.ServiceProvider.GetRequiredService<ISessionRepository>(),
                scope.ServiceProvider.GetRequiredService<ITranscriptionRepository>(),
                scope.ServiceProvider.GetRequiredService<IMemoRepository>(),
                scope.ServiceProvider.GetRequiredService<IMemoGeneratorService>());

            await memoProcessor.ProcessAsync(body.SessionId);
            await args.CompleteMessageAsync(args.Message);
        };

        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Service Bus processing error");
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }

        await processor.StopProcessingAsync();
    }

    private sealed record MemoJob(string SessionId);
}
```

**Step 4: Wire up Worker Program.cs**

```csharp
// backend/src/MyMemo.Worker/Program.cs
using MyMemo.Shared.Database;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;
using MyMemo.Worker.Workers;

var builder = Host.CreateApplicationBuilder(args);

// Database
var dbConnectionString = builder.Configuration.GetConnectionString("Database")
    ?? "Data Source=mymemo.db";
builder.Services.AddSingleton<IDbConnectionFactory>(new SqliteConnectionFactory(dbConnectionString));

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<IChunkRepository, ChunkRepository>();
builder.Services.AddScoped<ITranscriptionRepository, TranscriptionRepository>();
builder.Services.AddScoped<IMemoRepository, MemoRepository>();

// Azure services
builder.Services.Configure<AzureBlobOptions>(builder.Configuration.GetSection("AzureBlob"));
builder.Services.Configure<AzureServiceBusOptions>(builder.Configuration.GetSection("AzureServiceBus"));
builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection("AzureOpenAI"));
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
builder.Services.AddSingleton<IQueueService, QueueService>();
builder.Services.AddSingleton<IWhisperService, WhisperService>();
builder.Services.AddSingleton<IMemoGeneratorService, MemoGeneratorService>();

// Workers
builder.Services.AddHostedService<TranscriptionWorker>();
builder.Services.AddHostedService<MemoGenerationWorker>();

var host = builder.Build();

// Initialize database
var dbFactory = host.Services.GetRequiredService<IDbConnectionFactory>();
await DatabaseInitializer.Initialize(dbFactory);

host.Run();
```

**Step 5: Run tests**

```bash
cd backend && dotnet test tests/MyMemo.Worker.Tests -v n
```

Expected: PASS (5 tests)

**Step 6: Run full test suite**

```bash
cd backend && dotnet test -v n
```

Expected: ALL PASS

**Step 7: Commit**

```bash
git add backend/src/MyMemo.Worker/ backend/tests/MyMemo.Worker.Tests/
git commit -m "feat(backend): add memo generation worker with GPT-4.1 Nano"
```

---

## Task 11: Configuration, Docker & CI

**Files:**
- Create: `backend/src/MyMemo.Api/appsettings.json`
- Create: `backend/src/MyMemo.Api/Dockerfile`
- Create: `backend/src/MyMemo.Worker/appsettings.json`
- Create: `backend/src/MyMemo.Worker/Dockerfile`
- Create: `backend/docker-compose.yml`
- Create: `.github/workflows/backend-ci.yml`

**Step 1: Create API appsettings.json**

```json
// backend/src/MyMemo.Api/appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "Database": "Data Source=mymemo.db"
  },
  "Clerk": {
    "Domain": ""
  },
  "AzureBlob": {
    "ConnectionString": "",
    "ContainerName": "audio-chunks"
  },
  "AzureServiceBus": {
    "ConnectionString": "",
    "TranscriptionQueueName": "transcription-jobs",
    "MemoGenerationQueueName": "memo-generation"
  },
  "AzureOpenAI": {
    "Endpoint": "",
    "ApiKey": "",
    "WhisperDeployment": "whisper-1",
    "GptDeployment": "gpt-4.1-nano"
  }
}
```

**Step 2: Create Worker appsettings.json**

```json
// backend/src/MyMemo.Worker/appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "ConnectionStrings": {
    "Database": "Data Source=mymemo.db"
  },
  "AzureBlob": {
    "ConnectionString": "",
    "ContainerName": "audio-chunks"
  },
  "AzureServiceBus": {
    "ConnectionString": "",
    "TranscriptionQueueName": "transcription-jobs",
    "MemoGenerationQueueName": "memo-generation"
  },
  "AzureOpenAI": {
    "Endpoint": "",
    "ApiKey": "",
    "WhisperDeployment": "whisper-1",
    "GptDeployment": "gpt-4.1-nano"
  }
}
```

**Step 3: Create API Dockerfile**

```dockerfile
# backend/src/MyMemo.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/MyMemo.Shared/MyMemo.Shared.csproj", "src/MyMemo.Shared/"]
COPY ["src/MyMemo.Api/MyMemo.Api.csproj", "src/MyMemo.Api/"]
RUN dotnet restore "src/MyMemo.Api/MyMemo.Api.csproj"
COPY src/ src/
RUN dotnet publish "src/MyMemo.Api/MyMemo.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "MyMemo.Api.dll"]
```

**Step 4: Create Worker Dockerfile**

```dockerfile
# backend/src/MyMemo.Worker/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/MyMemo.Shared/MyMemo.Shared.csproj", "src/MyMemo.Shared/"]
COPY ["src/MyMemo.Worker/MyMemo.Worker.csproj", "src/MyMemo.Worker/"]
RUN dotnet restore "src/MyMemo.Worker/MyMemo.Worker.csproj"
COPY src/ src/
RUN dotnet publish "src/MyMemo.Worker/MyMemo.Worker.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MyMemo.Worker.dll"]
```

**Step 5: Create docker-compose.yml for local dev**

```yaml
# backend/docker-compose.yml
services:
  api:
    build:
      context: .
      dockerfile: src/MyMemo.Api/Dockerfile
    ports:
      - "5000:8080"
    env_file:
      - .env
    depends_on:
      - azurite

  worker:
    build:
      context: .
      dockerfile: src/MyMemo.Worker/Dockerfile
    env_file:
      - .env
    depends_on:
      - azurite

  azurite:
    image: mcr.microsoft.com/azure-storage/azurite
    ports:
      - "10000:10000"
      - "10001:10001"
      - "10002:10002"
```

**Step 6: Create GitHub Actions CI workflow**

```yaml
# .github/workflows/backend-ci.yml
name: Backend CI

on:
  push:
    paths: ['backend/**']
  pull_request:
    paths: ['backend/**']

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: backend
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --no-build --verbosity normal
```

**Step 7: Add .env.example**

```bash
# backend/.env.example
ConnectionStrings__Database=Data Source=mymemo.db
Clerk__Domain=your-domain.clerk.accounts.dev
AzureBlob__ConnectionString=DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://azurite:10000/devstoreaccount1
AzureServiceBus__ConnectionString=
AzureOpenAI__Endpoint=
AzureOpenAI__ApiKey=
```

**Step 8: Verify Docker build (optional)**

```bash
cd backend && docker compose build
```

**Step 9: Commit**

```bash
git add backend/src/MyMemo.Api/appsettings.json backend/src/MyMemo.Api/Dockerfile
git add backend/src/MyMemo.Worker/appsettings.json backend/src/MyMemo.Worker/Dockerfile
git add backend/docker-compose.yml backend/.env.example
git add .github/workflows/backend-ci.yml
git commit -m "feat(backend): add Docker, configuration, and CI pipeline"
```

---

## Summary

| Task | Description | Tests |
|------|-------------|-------|
| 1 | Scaffold .NET solution & projects | Build verification |
| 2 | Database schema & entity models | Build verification |
| 3 | Connection factory & repositories | ~12 unit tests |
| 4 | Azure service wrappers | Build verification |
| 5 | Clerk JWT auth middleware | 2 integration tests |
| 6 | Session CRUD endpoints | 4 integration tests |
| 7 | Chunk upload endpoint | 1 integration test |
| 8 | Finalize & memo endpoints | 2 integration tests |
| 9 | Transcription worker | 3 unit tests |
| 10 | Memo generation worker | 2 unit tests |
| 11 | Docker, config & CI | Build/compose verification |

**Total estimated tests:** ~26

**Key decisions:**
- Dapper + Microsoft.Data.Sqlite for DB (swap to Turso libSQL client for production)
- Interface-based services for testability
- In-memory SQLite for repository tests
- NSubstitute mocks for service-level tests
- WebApplicationFactory with test auth handler for endpoint tests
