using Microsoft.Extensions.Options;
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
builder.Services.Configure<StorageQueueOptions>(builder.Configuration.GetSection("StorageQueue"));
builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection("AzureOpenAI"));
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
builder.Services.AddSingleton<IQueueService, QueueService>();
builder.Services.AddScoped<IMemoTriggerService, MemoTriggerService>();
builder.Services.AddSingleton<IWhisperService, WhisperService>();
builder.Services.AddSingleton<IMemoGeneratorService, MemoGeneratorService>();

// Workers
builder.Services.AddHostedService<TranscriptionWorker>();
builder.Services.AddHostedService<MemoGenerationWorker>();

var host = builder.Build();

// Initialize database
var dbFactory = host.Services.GetRequiredService<IDbConnectionFactory>();
await DatabaseInitializer.Initialize(dbFactory);

// Log configured model names
var openAiOptions = host.Services.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
logger.LogInformation("Whisper deployment: {WhisperDeployment}, GPT deployment: {GptDeployment}",
    openAiOptions.WhisperDeployment, openAiOptions.GptDeployment);

host.Run();
