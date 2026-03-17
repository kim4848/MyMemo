using MyMemo.Api.Auth;
using MyMemo.Api.Endpoints;
using MyMemo.Shared.Database;
using MyMemo.Shared.Database.Turso;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5173"];
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddClerkAuth(builder.Configuration);

// Database — Azure SQL in production, Turso as fallback, SQLite locally
var sqlConnectionString = builder.Configuration.GetConnectionString("SqlDatabase");
var tursoUrl   = builder.Configuration["Turso:Url"];
var tursoToken = builder.Configuration["Turso:AuthToken"];

IDbConnectionFactory dbFactory;
Microsoft.Data.Sqlite.SqliteConnection? keepAliveConnection = null;

if (!string.IsNullOrWhiteSpace(sqlConnectionString))
{
    dbFactory = new SqlServerConnectionFactory(sqlConnectionString);
}
else if (!string.IsNullOrWhiteSpace(tursoUrl))
{
    if (string.IsNullOrWhiteSpace(tursoToken))
        throw new InvalidOperationException("Turso:AuthToken is required when Turso:Url is set.");
    dbFactory = new TursoConnectionFactory(tursoUrl, tursoToken!);
}
else
{
    var localCs = builder.Configuration.GetConnectionString("Database") ?? "Data Source=mymemo.db";
    dbFactory = new SqliteConnectionFactory(localCs);
    // Keep a connection alive for in-memory SQLite (shared cache requires one open connection)
    if (localCs.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase))
    {
        keepAliveConnection = new Microsoft.Data.Sqlite.SqliteConnection(localCs);
        keepAliveConnection.Open();
    }
}
builder.Services.AddSingleton(dbFactory);

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<IChunkRepository, ChunkRepository>();
builder.Services.AddScoped<ITranscriptionRepository, TranscriptionRepository>();
builder.Services.AddScoped<IMemoRepository, MemoRepository>();
builder.Services.AddScoped<IInfographicRepository, InfographicRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();

// Azure services
builder.Services.Configure<AzureBlobOptions>(builder.Configuration.GetSection("AzureBlob"));
builder.Services.Configure<StorageQueueOptions>(builder.Configuration.GetSection("StorageQueue"));
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();

builder.Services.AddSingleton<IQueueService, QueueService>();
builder.Services.AddScoped<IMemoTriggerService, MemoTriggerService>();

var app = builder.Build();

// Initialize database in background so health probe responds immediately
var dbReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
_ = Task.Run(async () =>
{
    try
    {
        await DatabaseInitializer.Initialize(app.Services.GetRequiredService<IDbConnectionFactory>());
        dbReady.SetResult();
    }
    catch (Exception ex)
    {
        dbReady.SetException(ex);
    }
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();

app.UseExceptionHandler(err => err.Run(async context =>
{
    context.Response.StatusCode = 500;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
}));

app.UseAuthentication();
app.UseAuthorization();

// Liveness probe — always responds (container is alive)
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
// Readiness probe — only healthy after DB init completes
app.MapGet("/ready", () => dbReady.Task.IsCompletedSuccessfully
    ? Results.Ok(new { status = "ready" })
    : Results.StatusCode(503));
SessionEndpoints.Map(app);
ChunkEndpoints.Map(app);
MemoEndpoints.Map(app);
InfographicEndpoints.Map(app);
TagEndpoints.Map(app);

// Ensure DB is initialized before accepting traffic
await dbReady.Task;

app.Run();

public partial class Program { }
