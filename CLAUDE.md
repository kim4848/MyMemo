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
| `web/`       | React 19, TypeScript, Vite, Tailwind CSS v4, Zustand, Web Audio API + MediaRecorder    |
| `desktop/`   | Electron (V2 feature — not yet implemented)                                            |
| `backend/`   | .NET 8 Minimal API, Azure Container Apps, Azure Service Bus, Azure Blob Storage, Turso |
| AI           | Azure OpenAI — Whisper (STT), GPT-4.1 Nano (memo generation)                          |
| Auth         | Clerk (frontend + backend JWT validation)                                              |
| Database     | Turso (libSQL/hosted SQLite), SQLite for local dev                                     |
| Infra        | Azure Bicep (IaC), Docker (Alpine multi-stage), GitHub Actions CI/CD                   |

## Code Conventions

From `.editorconfig`:
- **Indent:** 2 spaces (default), 4 spaces for `.cs` and `.py`, tabs for `Makefile`
- **Line endings:** LF
- **Encoding:** UTF-8
- **Trailing whitespace:** trimmed (except `.md`)
- **Final newline:** always

## Project Structure

```
MyMemo/
├── web/                        # React SPA frontend
│   ├── src/
│   │   ├── components/         # UI components (AudioLevelIndicator, Layout, MemoViewer, etc.)
│   │   ├── pages/              # DashboardPage, LoginPage, RecorderPage, SessionDetailPage
│   │   ├── stores/             # Zustand stores (recorder, sessions)
│   │   ├── api/                # API client
│   │   ├── services/           # Business logic (recorder, sessions)
│   │   ├── hooks/              # Custom hooks (audio levels)
│   │   └── lib/                # Utility functions
│   └── netlify.toml            # Netlify config with API proxy
├── backend/
│   ├── src/
│   │   ├── MyMemo.Api/         # ASP.NET Core Minimal API
│   │   ├── MyMemo.Worker/      # Background processing (transcription + memo generation)
│   │   └── MyMemo.Shared/      # Shared library (models, repos, services, database)
│   ├── tests/
│   │   ├── MyMemo.Api.Tests/
│   │   ├── MyMemo.Worker.Tests/
│   │   └── MyMemo.Shared.Tests/
│   ├── docker-compose.yml      # Local dev: API + Worker + Azurite
│   └── MyMemo.sln
├── desktop/                    # Electron app (V2 — placeholder only)
├── infra/                      # Azure Bicep IaC templates
├── docs/
│   ├── technical-specification.md
│   └── plans/                  # Implementation planning docs
└── .github/workflows/          # CI/CD pipelines
```

## Build & Development

### Frontend (`web/`)

```bash
cd web
npm install
npm run dev          # Vite dev server
npm run build        # tsc -b && vite build
npm run lint         # ESLint
npm run test         # Vitest
npm run test:watch   # Vitest watch mode
```

### Backend (`backend/`)

```bash
cd backend
dotnet restore
dotnet build
dotnet test

# Local development with Docker (API + Worker + Azurite)
docker-compose up
```

### CI/CD

- `backend-ci.yml` — Runs on backend changes: restore → build → test
- `backend-deploy.yml` — On main: build → test → push Docker images to ACR → deploy to Azure Container Apps

## Database & Migrations

Backend uses **Dapper** (micro-ORM with raw SQL) — NOT Entity Framework Core. There is no auto-generated migration tooling.

**Models:** User, Session, Chunk, Transcription, Memo (in `backend/src/MyMemo.Shared/Models/`)

**Database factories:**
- `SqliteConnectionFactory` — local development
- `TursoConnectionFactory` — production (custom ADO.NET wrapper for Turso HTTP API)

When adding or changing a model property, you MUST update all locations:

1. **Model class** — `backend/src/MyMemo.Shared/Models/{Entity}.cs`
2. **Schema SQL** — `backend/src/MyMemo.Shared/Database/schema.sql` (the `CREATE TABLE` statement)
3. **DatabaseInitializer** — `backend/src/MyMemo.Shared/Database/DatabaseInitializer.cs` (add an `ALTER TABLE` migration for existing databases, wrapped in try-catch to skip if column already exists)
4. **Repository SQL** — update any Dapper queries in `backend/src/MyMemo.Shared/Repositories/` that need the new column

The `DatabaseInitializer` runs on startup in both API and Worker. It creates tables from `schema.sql` and applies incremental `ALTER TABLE` migrations.

## Backend Services

| Service               | Purpose                                          |
|-----------------------|--------------------------------------------------|
| `WhisperService`      | Azure OpenAI Whisper STT                         |
| `BlobStorageService`  | Azure Blob Storage for audio chunks              |
| `QueueService`        | Azure Service Bus job queue                      |
| `MemoGeneratorService`| GPT-4.1 Nano memo generation                     |
| `MemoTriggerService`  | Orchestrates memo generation on session finalize  |

## API Endpoints

Defined in `backend/src/MyMemo.Api/Endpoints/`:
- `SessionEndpoints` — Session CRUD
- `ChunkEndpoints` — `POST /api/sessions/{sessionId}/chunks` (audio upload)
- `MemoEndpoints` — Memo retrieval

## Key References

- `docs/technical-specification.md` — full spec (Danish): API endpoints, data model, LLM prompts, infra, cost estimates, phased rollout
- `docs/plans/` — implementation planning documents
