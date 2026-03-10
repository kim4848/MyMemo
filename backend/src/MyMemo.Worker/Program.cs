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

// Azure services
builder.Services.Configure<AzureBlobOptions>(builder.Configuration.GetSection("AzureBlob"));
builder.Services.Configure<StorageQueueOptions>(builder.Configuration.GetSection("StorageQueue"));
builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection("AzureOpenAI"));
builder.Services.Configure<AzureSpeechOptions>(builder.Configuration.GetSection("AzureSpeech"));
builder.Services.Configure<TranscriptionOptions>(builder.Configuration.GetSection("Transcription"));
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
builder.Services.AddSingleton<IQueueService, QueueService>();
builder.Services.AddScoped<IMemoTriggerService, MemoTriggerService>();

// Feature toggle: select transcription provider based on config
var transcriptionProvider = builder.Configuration.GetValue<string>("Transcription:Provider") ?? "whisper";
if (transcriptionProvider == "azure-fast")
{
    builder.Services.AddHttpClient<IWhisperService, AzureFastTranscriptionService>();
}
else
{
    builder.Services.AddSingleton<IWhisperService, WhisperService>();
}

builder.Services.AddSingleton<IMemoGeneratorService, MemoGeneratorService>();
builder.Services.AddSingleton<IInfographicService, InfographicService>();

// Workers
builder.Services.AddHostedService<TranscriptionWorker>();
builder.Services.AddHostedService<MemoGenerationWorker>();
builder.Services.AddHostedService<InfographicGenerationWorker>();

var host = builder.Build();

// Initialize database
await DatabaseInitializer.Initialize(host.Services.GetRequiredService<IDbConnectionFactory>());

// Log configured model names
var openAiOptions = host.Services.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
var txOptions = host.Services.GetRequiredService<IOptions<TranscriptionOptions>>().Value;
logger.LogInformation("Transcription provider: {Provider}, Whisper deployment: {WhisperDeployment}, GPT deployment: {GptDeployment}",
    txOptions.Provider, openAiOptions.WhisperDeployment, openAiOptions.GptDeployment);

host.Run();
