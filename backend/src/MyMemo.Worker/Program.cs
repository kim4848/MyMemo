using Microsoft.Extensions.Options;
using MyMemo.Shared.Database;
using MyMemo.Shared.Database.Turso;
using MyMemo.Shared.Repositories;
using MyMemo.Shared.Services;
using MyMemo.Worker.Workers;

var builder = Host.CreateApplicationBuilder(args);

// Database — use Turso in production, SQLite locally
var tursoUrl   = builder.Configuration["Turso:Url"];
var tursoToken = builder.Configuration["Turso:AuthToken"];

if (!string.IsNullOrWhiteSpace(tursoUrl) && string.IsNullOrWhiteSpace(tursoToken))
    throw new InvalidOperationException("Turso:AuthToken is required when Turso:Url is set.");

IDbConnectionFactory dbFactory = !string.IsNullOrWhiteSpace(tursoUrl)
    ? new TursoConnectionFactory(tursoUrl, tursoToken!)
    : new SqliteConnectionFactory(builder.Configuration.GetConnectionString("Database") ?? "Data Source=mymemo.db");
builder.Services.AddSingleton(dbFactory);

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<IChunkRepository, ChunkRepository>();
builder.Services.AddScoped<ITranscriptionRepository, TranscriptionRepository>();
builder.Services.AddScoped<IMemoRepository, MemoRepository>();
builder.Services.AddScoped<IInfographicRepository, InfographicRepository>();
builder.Services.AddScoped<IBatchTranscriptionJobRepository, BatchTranscriptionJobRepository>();

// Azure services
builder.Services.Configure<AzureBlobOptions>(builder.Configuration.GetSection("AzureBlob"));
builder.Services.Configure<StorageQueueOptions>(builder.Configuration.GetSection("StorageQueue"));
builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection("AzureOpenAI"));
builder.Services.Configure<AzureSpeechOptions>(builder.Configuration.GetSection("AzureSpeech"));
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
builder.Services.AddSingleton<IQueueService, QueueService>();
builder.Services.AddScoped<IMemoTriggerService, MemoTriggerService>();
builder.Services.AddSingleton<IWhisperService, WhisperService>();
builder.Services.AddSingleton<IMemoGeneratorService, MemoGeneratorService>();
builder.Services.AddSingleton<IInfographicService, InfographicService>();
builder.Services.AddSingleton<IAudioConverterService, AudioConverterService>();
builder.Services.AddSingleton<ISpeechBatchTranscriptionService, SpeechBatchTranscriptionService>();

// Workers
builder.Services.AddHostedService<TranscriptionWorker>();
builder.Services.AddHostedService<MemoGenerationWorker>();
builder.Services.AddHostedService<InfographicGenerationWorker>();
builder.Services.AddHostedService<BatchTranscriptionPollWorker>();

var host = builder.Build();

// Initialize database
await DatabaseInitializer.Initialize(host.Services.GetRequiredService<IDbConnectionFactory>());

// Log configured model names
var openAiOptions = host.Services.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
logger.LogInformation("Whisper deployment: {WhisperDeployment}, GPT deployment: {GptDeployment}",
    openAiOptions.WhisperDeployment, openAiOptions.GptDeployment);

host.Run();
