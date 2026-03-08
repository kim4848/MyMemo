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

// Database — use Turso in production, SQLite locally
var tursoUrl   = builder.Configuration["Turso:Url"];
var tursoToken = builder.Configuration["Turso:AuthToken"];

if (!string.IsNullOrWhiteSpace(tursoUrl) && string.IsNullOrWhiteSpace(tursoToken))
    throw new InvalidOperationException("Turso:AuthToken is required when Turso:Url is set.");

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

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<IChunkRepository, ChunkRepository>();
builder.Services.AddScoped<ITranscriptionRepository, TranscriptionRepository>();
builder.Services.AddScoped<IMemoRepository, MemoRepository>();
builder.Services.AddScoped<IInfographicRepository, InfographicRepository>();

// Azure services
builder.Services.Configure<AzureBlobOptions>(builder.Configuration.GetSection("AzureBlob"));
builder.Services.Configure<StorageQueueOptions>(builder.Configuration.GetSection("StorageQueue"));
builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection("AzureOpenAI"));
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();

builder.Services.AddSingleton<IQueueService, QueueService>();
builder.Services.AddScoped<IMemoTriggerService, MemoTriggerService>();

var app = builder.Build();

// Initialize database
await DatabaseInitializer.Initialize(app.Services.GetRequiredService<IDbConnectionFactory>());

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

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
SessionEndpoints.Map(app);
ChunkEndpoints.Map(app);
MemoEndpoints.Map(app);
InfographicEndpoints.Map(app);

app.Run();

public partial class Program { }
