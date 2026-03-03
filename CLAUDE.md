# MyMemo

Live transcription app for meetings and conversations, primarily in Danish. Records audio, transcribes via Whisper, and generates cleaned memos via LLM.

## Architecture

```
Frontend (React SPA on Netlify)
  Audio capture → 5-min WebM/Opus chunks → upload to Azure Blob Storage + POST to API

API Service (.NET 8 on Azure Container Apps)
  Validates auth → stores chunk metadata in Turso → enqueues job on Azure Service Bus

Worker Service (Azure Container Apps)
  Dequeues job → downloads chunk from Blob → Azure OpenAI Whisper STT → stores transcription in Turso

Memo Generation (part of Worker)
  On session finalize: concatenates all chunk transcriptions → Azure OpenAI GPT-4.1 Nano generates memo
  Two output modes:
    "full"    — cleaned transcription (grammar, punctuation, structure)
    "summary" — structured meeting notes (key points, decisions, action items)
```

## Tech Stack

| Component    | Stack                                                                                  |
|--------------|----------------------------------------------------------------------------------------|
| `web/`       | React 18+ TypeScript, Tailwind CSS, Zustand, Web Audio API + MediaRecorder, Netlify    |
| `desktop/`   | Electron (V2 feature)                                                                  |
| `backend/`   | .NET 8 Minimal API, Azure Container Apps, Azure Service Bus, Azure Blob Storage, Turso |
| AI           | Azure OpenAI — Whisper (STT), GPT-4.1 Nano (memo generation)                          |
| Auth         | Clerk (V1)                                                                             |
| Database     | Turso (libSQL/hosted SQLite)                                                           |

## Code Conventions

From `.editorconfig`:
- **Indent:** 2 spaces (default), 4 spaces for `.cs` and `.py`, tabs for `Makefile`
- **Line endings:** LF
- **Encoding:** UTF-8
- **Trailing whitespace:** trimmed (except `.md`)
- **Final newline:** always

## Build & Development

Project is in initialization phase — no runnable code yet. Commands will be added as components are scaffolded. See component READMEs:
- `web/README.md`
- `backend/README.md`
- `desktop/README.md`

## Database & Migrations

Backend uses **Dapper** (micro-ORM with raw SQL) — NOT Entity Framework Core. There is no auto-generated migration tooling.

When adding or changing a model property, you MUST update all three locations:

1. **Model class** — `backend/src/MyMemo.Shared/Models/{Entity}.cs`
2. **Schema SQL** — `backend/src/MyMemo.Shared/Database/schema.sql` (the `CREATE TABLE` statement)
3. **DatabaseInitializer** — `backend/src/MyMemo.Shared/Database/DatabaseInitializer.cs` (add an `ALTER TABLE` migration for existing databases, wrapped in try-catch to skip if column already exists)
4. **Repository SQL** — update any Dapper queries in `backend/src/MyMemo.Shared/Repositories/` that need the new column

The `DatabaseInitializer` runs on startup in both API and Worker. It creates tables from `schema.sql` and applies incremental `ALTER TABLE` migrations.

## Key References

- `docs/technical-specification.md` — full spec (Danish): API endpoints, data model, LLM prompts, infra, cost estimates, phased rollout
