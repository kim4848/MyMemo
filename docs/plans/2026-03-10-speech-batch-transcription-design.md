# Azure Speech Batch Transcription with Diarization

## Summary

Add Azure Speech Services batch transcription as an alternative to Whisper, giving users per-session choice between simple transcription (Whisper) and speaker-diarized transcription (Azure Speech). Deployed in Sweden Central.

## Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Whisper vs Speech | User chooses per session | Different use cases: solo notes vs multi-speaker meetings |
| Diarization output | Structured JSON + readable text | JSON for UI rendering, readable text for memo pipeline |
| API style | Batch transcription (async) | Submit → poll → collect; fits queue architecture |
| Audio source | SAS URL from existing Blob Storage | No re-upload; Speech Service reads directly |
| Audio format | Server-side ffmpeg WebM→WAV conversion | WebM not supported by Speech API; ffmpeg in Docker keeps frontend unchanged; works on all browsers including iOS |
| Worker architecture | Two-phase (submit + poll) | Clean separation; no long-running queue holds |

## Infrastructure

### New Bicep module: `modules/speech.bicep`

- Resource: `Microsoft.CognitiveServices/accounts`, kind `SpeechServices`
- Location: `swedencentral` (reuses `openAiLocation` param)
- SKU: S0
- Outputs: endpoint, account name

### Worker Dockerfile

Add ffmpeg: `RUN apk add --no-cache ffmpeg`

### Worker Container App env vars

- `AzureSpeech__Endpoint` — Speech Services endpoint
- `AzureSpeech__ApiKey` — from `speechAccount.listKeys().key1`
- `AzureSpeech__Region` — `swedencentral`

## Backend Changes

### Configuration

New `AzureSpeechOptions` in `ServiceConfiguration.cs`:

```csharp
public sealed class AzureSpeechOptions
{
    public required string Endpoint { get; init; }
    public required string ApiKey { get; init; }
    public string Region { get; init; } = "swedencentral";
}
```

### Session Model

Add `transcription_mode` column to `sessions` table:
- Values: `"whisper"` (default), `"speech"`
- Set at session creation time
- Passed through queue message to worker

### Database: new table `batch_transcription_jobs`

```sql
CREATE TABLE IF NOT EXISTS batch_transcription_jobs (
    id TEXT PRIMARY KEY,
    chunk_id TEXT NOT NULL,
    session_id TEXT NOT NULL,
    azure_job_id TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'submitted',
    created_at TEXT NOT NULL,
    completed_at TEXT
);
```

Migration in `DatabaseInitializer`:
- `ALTER TABLE sessions ADD COLUMN transcription_mode TEXT DEFAULT 'whisper'`
- `CREATE TABLE batch_transcription_jobs ...`

### New Services

**`AudioConverterService`**
- `ConvertToWavAsync(Stream input) → string tempWavPath`
- Shells out to ffmpeg: `-i input.webm -ar 16000 -ac 1 -f wav output.wav`
- Caller responsible for deleting temp file

**`SpeechBatchTranscriptionService` (implements `ISpeechBatchTranscriptionService`)**
- Uses Azure Speech REST API (`/speechtotext/v3.2/transcriptions`)
- `SubmitAsync(string sasUrl, string language)` → returns Azure job ID
  - Enables diarization, sets locale to `da-DK`
- `GetStatusAsync(string jobId)` → returns status enum
- `GetResultAsync(string jobId)` → returns structured result with speaker segments

**`BlobStorageService` additions**
- `GenerateSasUrl(string blobPath, TimeSpan expiry)` → SAS URL for read access
- `UploadAsync(string blobPath, Stream content)` — for uploading converted WAV

### Worker Changes

**Phase 1 — TranscriptionWorker (modified)**

When `transcription_mode == "speech"`:
1. Download WebM from blob
2. Convert to WAV via `AudioConverterService`
3. Upload WAV to blob as `{path}.wav`
4. Generate SAS URL for WAV blob (24h expiry)
5. Submit to `SpeechBatchTranscriptionService.SubmitAsync()`
6. Store batch job in `batch_transcription_jobs` table
7. Update chunk status to `"batch_submitted"`
8. Delete temp WAV file

When `transcription_mode == "whisper"`: unchanged existing flow.

**Phase 2 — New `BatchTranscriptionPollWorker` (BackgroundService)**

Runs on 30-second timer:
1. Query `batch_transcription_jobs` where status = `"submitted"`
2. For each job, call `GetStatusAsync()`
3. If succeeded:
   - Fetch results via `GetResultAsync()`
   - Parse speaker diarization into structured JSON (speaker ID, start, end, text)
   - Generate readable text: `"Speaker 1: ...\nSpeaker 2: ..."`
   - Store in `Transcription` record (structured JSON in `word_timestamps`, readable in `raw_text`)
   - Update chunk status to `"transcribed"`
   - Trigger memo generation via `MemoTriggerService`
   - Mark batch job as `"completed"`
4. If failed: mark batch job and chunk as `"failed"`

### API Changes

- `SessionEndpoints`: accept `transcriptionMode` in create session request
- `TranscriptionJob` queue message: include `transcriptionMode` field

### Frontend Changes

- Recorder page: toggle for "With speaker identification" (sets `transcription_mode: "speech"`)
- Session detail page: render speaker labels when diarization data present

## Data Flow

```
Frontend (toggle: simple / with speakers)
  → POST /api/sessions { transcriptionMode: "speech" }
  → Audio chunks uploaded (WebM/Opus, same as before)
  → Queue message includes transcriptionMode

Worker Phase 1 (queue-triggered):
  whisper mode → existing Whisper flow (sync, unchanged)
  speech mode  → download WebM → ffmpeg → WAV → upload WAV → SAS URL
               → submit batch job → store job ID → done

Worker Phase 2 (timer, 30s):
  → query pending batch jobs
  → poll Azure Speech for status
  → on success: parse diarized result → store transcription → trigger memo
  → on failure: mark failed
```
