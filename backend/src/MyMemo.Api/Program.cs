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
