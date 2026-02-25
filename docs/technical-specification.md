# Transskriberings-App — Teknisk Specifikation

**Version:** 0.1 DRAFT
**Dato:** 2026-02-25
**Forfatter:** Kim Aagaard / Claude

-----

## 1. Produktoverblik

Minimalistisk webapplikation til live-transkribering af møder og samtaler, primært på dansk. Applikationen optager lyd i 5-minutters bidder, transkriberer løbende via Azure OpenAI Whisper, og producerer et renskrevet memo via LLM.

### 1.1 Kernefunktionalitet

- Optag lyd fra mikrofon, skærm (Teams/Zoom-lyd), eller begge samtidig
- Automatisk chunking i ~5 min. intervaller
- Løbende transkribering via kø-baseret arkitektur
- LLM-renskrivning med konfigurerbar output (fuld transkription vs. opsummering)
- Multi-user med autentificering
- Max sessionsvarighed: 2 timer (24 chunks à 5 min.)

### 1.2 Målplatforme

| Fase   | Platform               | Audio-capture                                                             |
|--------|------------------------|---------------------------------------------------------------------------|
| **V1** | Web (React på Netlify) | Mikrofon via `getUserMedia`, systemlyd via `getDisplayMedia`              |
| **V2** | Electron desktop-app   | Mikrofon + system audio via `desktopCapturer` (ingen screen-share dialog) |

-----

## 2. Arkitekturoverblik

```
┌─────────────────────────────────────────────────────────┐
│  Frontend (Netlify)                                     │
│  React SPA                                              │
│  ┌─────────┐  ┌──────────┐  ┌────────────────────────┐  │
│  │ Recorder │→ │ Chunker  │→ │ Upload til Blob Storage│  │
│  │ (5 min)  │  │ (WebM)   │  │ + API kald             │  │
│  └─────────┘  └──────────┘  └────────────────────────┘  │
└────────────────────────┬────────────────────────────────┘
                         │ HTTPS
┌────────────────────────▼────────────────────────────────┐
│  Backend (Azure)                                        │
│                                                         │
│  ┌──────────────────┐   ┌─────────────────────────────┐ │
│  │ API              │   │ Queue Worker                 │ │
│  │ (Container App)  │   │ (Container App / Function)   │ │
│  │                  │   │                              │ │
│  │ • Auth           │   │ • Hent chunk fra Blob        │ │
│  │ • Upload chunk   │   │ • Whisper transkribering     │ │
│  │ • Session CRUD   │   │ • Gem resultat i Turso       │ │
│  │ • Hent status    │   │ • Opdater session-status     │ │
│  │ • Trigger memo   │   │                              │ │
│  └──────┬───────────┘   └──────┬──────────────────────┘ │
│         │                      │                        │
│  ┌──────▼──────────────────────▼──────────────────────┐ │
│  │ Azure Blob Storage          Azure Service Bus Queue │ │
│  │ (audio chunks)              (transkriberingsopgaver)│ │
│  └─────────────────────────────────────────────────────┘ │
│                                                         │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────┐  │
│  │ Azure OpenAI │  │ Azure OpenAI │  │ Turso DB      │  │
│  │ Whisper      │  │ GPT-4.1 Nano │  │ (SQLite edge) │  │
│  │ (STT)        │  │ (renskrivning│  │               │  │
│  └──────────────┘  └──────────────┘  └───────────────┘  │
└─────────────────────────────────────────────────────────┘
```

-----

## 3. Frontend

### 3.1 Tech-stack

- **React 18+** med TypeScript
- **Hosting:** Netlify (statisk SPA)
- **Styling:** Tailwind CSS (minimalistisk UI)
- **State:** Zustand eller React Context
- **Audio:** Web Audio API + MediaRecorder API

### 3.2 Sider/Views

| View               | Beskrivelse                     |
|--------------------|---------------------------------|
| **Login**          | Auth-flow (se sektion 6)        |
| **Dashboard**      | Liste over sessioner med status |
| **Recorder**       | Aktiv optagelse med live-status |
| **Session Detail** | Visning af transkription + memo |

### 3.3 Recorder — Audio Capture

#### Lydkilder

| Kilde             | Browser API                                                                                       | Bemærkning                                                       |
|-------------------|---------------------------------------------------------------------------------------------------|------------------------------------------------------------------|
| Mikrofon          | `navigator.mediaDevices.getUserMedia({ audio: true })`                                            | Kræver bruger-accept                                             |
| Systemlyd (Teams) | `navigator.mediaDevices.getDisplayMedia({ audio: true, video: true })`                            | Kræver at brugeren vælger skærm/vindue. Video-track kan droppes. |
| Begge             | Kombiner streams via `AudioContext.createMediaStreamSource()` + `MediaStreamAudioDestinationNode`  | Mix to kilder til én stream                                      |

#### Chunking-strategi

```
MediaRecorder (WebM/Opus, 128kbps)
        │
        ▼
  ondataavailable (hvert 5. minut via timeslice)
        │
        ▼
  Blob → Upload til Azure Blob Storage
        │
        ▼
  POST /api/chunks → Kø-besked i Service Bus
```

- **Format:** WebM med Opus codec (native browser-format, god komprimering)
- **Interval:** `timeslice: 300000` (5 min = 300.000 ms)
- **Filstørrelse estimat:** ~5 MB per 5-min chunk ved 128kbps
- **Max chunks per session:** 24 (2 timer)
- **Total max storage per session:** ~120 MB

#### Offline-resilience

- Chunks gemmes midlertidigt i IndexedDB hvis upload fejler
- Retry-logik med exponential backoff
- UI viser upload-status per chunk (pending/uploading/done/failed)

### 3.4 UI-komponenter (Recorder View)

```
┌─────────────────────────────────────┐
│  📝 Ny session                      │
│                                     │
│  Lydkilde: [Mikrofon ▾]            │
│            ☐ Inkludér systemlyd     │
│                                     │
│  ● REC  00:23:45       [■ Stop]     │
│                                     │
│  Chunks:                            │
│  ✅ Chunk 1 (00:00–05:00) — done    │
│  ✅ Chunk 2 (05:00–10:00) — done    │
│  ✅ Chunk 3 (10:00–15:00) — done    │
│  🔄 Chunk 4 (15:00–20:00) — trans.. │
│  ⬆️  Chunk 5 (20:00–23:45) — rec..  │
│                                     │
│  Live preview:                      │
│  "...og så tænker jeg at vi bør..." │
│                                     │
│  [Afslut & generer memo]            │
└─────────────────────────────────────┘
```

-----

## 4. Backend

### 4.1 Tech-stack

- **Runtime:** .NET 8 (Minimal API)
- **Hosting:** Azure Container Apps (håndterer long-running workers bedre end Functions)
- **Queue:** Azure Service Bus
- **Storage:** Azure Blob Storage
- **Database:** Turso (libSQL, hosted SQLite)

### 4.2 Services

#### API Service (Container App #1)

Håndterer HTTP-requests fra frontend.

| Endpoint                           | Metode | Beskrivelse                               |
|------------------------------------|--------|-------------------------------------------|
| `POST /api/auth/login`             | POST   | Login/token                               |
| `POST /api/sessions`               | POST   | Opret ny session                          |
| `GET /api/sessions`                | GET    | List brugerens sessioner                  |
| `GET /api/sessions/{id}`           | GET    | Session detaljer + chunks + transkription |
| `POST /api/sessions/{id}/chunks`   | POST   | Upload audio chunk (multipart)            |
| `POST /api/sessions/{id}/finalize` | POST   | Afslut session, trigger memo-generering   |
| `GET /api/sessions/{id}/memo`      | GET    | Hent færdigt memo                         |
| `DELETE /api/sessions/{id}`        | DELETE | Slet session + tilhørende data            |

#### Chunk Upload Flow

```
1. Frontend POST /api/sessions/{id}/chunks
   → Body: multipart/form-data (audio blob + chunk metadata)

2. API Service:
   a. Validér auth + session ownership
   b. Upload blob til Azure Blob Storage:
      container: "audio-chunks"
      path: "{userId}/{sessionId}/{chunkIndex}.webm"
   c. Send besked til Service Bus queue "transcription-jobs":
      { sessionId, chunkIndex, blobPath, language: "da" }
   d. Opret chunk-record i Turso (status: "queued")
   e. Return 202 Accepted

3. Frontend poller GET /api/sessions/{id} for status-opdateringer
   (eller WebSocket/SSE for realtime — V2)
```

#### Worker Service (Container App #2)

Lytter på Service Bus queue og processerer transkriberingsjobs.

```
1. Modtag besked fra "transcription-jobs" queue
2. Download audio chunk fra Blob Storage
3. Kald Azure OpenAI Whisper API:
   - Model: whisper-1
   - Language: "da"
   - Response format: "verbose_json" (inkl. timestamps)
4. Gem rå transkription i Turso (chunks tabel)
5. Opdater chunk status → "transcribed"
6. Hvis alle chunks i sessionen er transcribed:
   → Send besked til "memo-generation" queue
```

#### Memo Generation (del af Worker)

```
1. Modtag besked fra "memo-generation" queue
2. Hent alle chunk-transkriptioner for sessionen (sorteret)
3. Sammensæt fuld transkription
4. Kald Azure OpenAI GPT-4.1 Nano:
   - System prompt baseret på session.outputMode:
     a. "full" → Renskrivning (grammatik, tegnsætning, struktur)
     b. "summary" → Opsummering med key points og action items
   - Input: Fuld sammenkædet transkription
   - Language instruction: Dansk output
5. Gem memo i Turso
6. Opdater session status → "completed"
```

### 4.3 LLM Prompts

#### Renskrivning (mode: "full")

```
Du er en professionel dansk transskribent. Renskiv følgende rå transkription.

Regler:
- Ret grammatik og stavefejl
- Tilføj korrekt tegnsætning
- Bevar talerens oprindelige ordvalg og tone
- Strukturer i afsnit med logiske pauser
- Marker tydeligt hvis noget er uhørbart: [uhørbart]
- Bevar tidsstempler som sektion-markører
- Output på dansk

Rå transkription:
{transcription}
```

#### Opsummering (mode: "summary")

```
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

Transkription:
{transcription}
```

-----

## 5. Datamodel (Turso/SQLite)

```sql
-- Brugere
CREATE TABLE users (
    id          TEXT PRIMARY KEY DEFAULT (lower(hex(randomblob(16)))),
    email       TEXT NOT NULL UNIQUE,
    name        TEXT NOT NULL,
    created_at  TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at  TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Sessioner
CREATE TABLE sessions (
    id          TEXT PRIMARY KEY DEFAULT (lower(hex(randomblob(16)))),
    user_id     TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    title       TEXT,                              -- Auto-genereret eller bruger-sat
    status      TEXT NOT NULL DEFAULT 'recording', -- recording | processing | completed | failed
    output_mode TEXT NOT NULL DEFAULT 'full',      -- full | summary
    audio_source TEXT NOT NULL DEFAULT 'microphone', -- microphone | system | both
    started_at  TEXT NOT NULL DEFAULT (datetime('now')),
    ended_at    TEXT,
    created_at  TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at  TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX idx_sessions_user ON sessions(user_id);

-- Audio chunks
CREATE TABLE chunks (
    id              TEXT PRIMARY KEY DEFAULT (lower(hex(randomblob(16)))),
    session_id      TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    chunk_index     INTEGER NOT NULL,               -- 0-baseret sekvens
    blob_path       TEXT NOT NULL,                   -- Azure Blob path
    duration_sec    INTEGER,                         -- Faktisk varighed
    status          TEXT NOT NULL DEFAULT 'uploaded', -- uploaded | queued | transcribing | transcribed | failed
    error_message   TEXT,
    created_at      TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at      TEXT NOT NULL DEFAULT (datetime('now')),
    UNIQUE(session_id, chunk_index)
);

CREATE INDEX idx_chunks_session ON chunks(session_id);

-- Transkriptioner (én per chunk)
CREATE TABLE transcriptions (
    id              TEXT PRIMARY KEY DEFAULT (lower(hex(randomblob(16)))),
    chunk_id        TEXT NOT NULL UNIQUE REFERENCES chunks(id) ON DELETE CASCADE,
    raw_text        TEXT NOT NULL,                   -- Rå Whisper output
    language        TEXT DEFAULT 'da',
    confidence      REAL,                            -- Gennemsnitlig confidence
    word_timestamps TEXT,                            -- JSON med ord-level timestamps
    created_at      TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Memos (ét per session)
CREATE TABLE memos (
    id              TEXT PRIMARY KEY DEFAULT (lower(hex(randomblob(16)))),
    session_id      TEXT NOT NULL UNIQUE REFERENCES sessions(id) ON DELETE CASCADE,
    output_mode     TEXT NOT NULL,                   -- full | summary
    content         TEXT NOT NULL,                   -- Renskrevet/opsummeret tekst (markdown)
    model_used      TEXT NOT NULL,                   -- F.eks. "gpt-4.1-nano"
    prompt_tokens   INTEGER,
    completion_tokens INTEGER,
    created_at      TEXT NOT NULL DEFAULT (datetime('now'))
);
```

-----

## 6. Autentificering

### Anbefalet: Auth0 eller Clerk

Multi-user kræver en solid auth-provider. Anbefaling:

| Mulighed               | Fordele                             | Ulemper               |
|------------------------|-------------------------------------|-----------------------|
| **Clerk**              | Simpel React SDK, god DX, hosted UI | Koster ved >5 users   |
| **Auth0**              | Velkendt, fleksibel, gratis tier    | Mere setup            |
| **Microsoft Entra ID** | Naturligt fit med Azure + Teams     | Mere enterprise-setup |

For V1 anbefales **Clerk** for hurtigste time-to-market, med mulighed for migration til Entra ID senere.

### Auth-flow

```
Frontend (Clerk React SDK)
    │
    │ Bearer token (JWT)
    ▼
API Service
    │ Validér JWT → Udtræk userId
    ▼
Turso (user lookup/creation)
```

-----

## 7. Speaker Diarization — Analyse

### Hvad det kræver

Speaker diarization (identificering af *hvem* der taler *hvornår*) er ikke inkluderet i Whisper. Det kræver en ekstra processing-pipeline.

### Tilgængelige muligheder

| Løsning                                   | Type                 | Dansk support                | Pris                                           | Kompleksitet                  |
|--------------------------------------------|----------------------|------------------------------|-------------------------------------------------|-------------------------------|
| **Azure AI Speech** (Speaker Diarization)  | Managed API          | Ja                           | ~$1.40/audio-time (conversation transcription)  | Lav — REST API kald           |
| **pyannote.audio**                         | Open source (Python) | Sprogagnostisk (lyd-baseret) | Gratis (model) + compute                        | Høj — kræver GPU, self-hosted |
| **AssemblyAI**                             | Managed API          | Ja                           | $0.65/audio-time                                | Lav — REST API                |
| **Deepgram**                               | Managed API          | Begrænset dansk              | $0.05/audio-time                                | Lav                           |

### Anbefaling

**Azure AI Speech** er det naturlige valg givet den eksisterende Azure-stack:

- Samme region, lav latency
- Integrerer med eksisterende auth/networking
- Understøtter dansk
- Kan køre som del af worker-pipeline efter Whisper

### Diarization-flow (V2 feature)

```
1. Worker modtager chunk
2. Send til Azure AI Speech (conversation transcription)
   → Returnerer segments med speaker labels: Speaker1, Speaker2, ...
3. Merge speaker labels med Whisper timestamps
4. Gem i transcriptions tabel som enriched format:
   [{ speaker: "Speaker1", start: 0.0, end: 4.2, text: "..." }, ...]
5. LLM prompt tilpasses til at inkludere speaker-skift
```

### Impact på arkitektur

- Worker skal håndtere et ekstra API-kald per chunk (~dobbelt procestid)
- Transcriptions-tabellen skal udvides med speaker-data (JSON-felt)
- Frontend skal kunne vise speaker-labels
- LLM prompts skal tilpasses til at respektere speaker-skift

### Estimeret merkostnad

For 2 timer audio: ~$2.80 (Azure AI Speech) oven i Whisper-omkostningen.

**Anbefaling:** Implementér som opt-in feature i V2. Arkitekturen er klar til det via det eksisterende `word_timestamps` JSON-felt i transcriptions-tabellen.

-----

## 8. Infrastruktur & Deployment

### Azure-ressourcer

| Ressource     | Service       | SKU/Tier                                            |
|---------------|---------------|-----------------------------------------------------|
| API           | Container App | Consumption (auto-scale 0→N)                        |
| Worker        | Container App | Consumption (auto-scale 0→N baseret på queue depth) |
| Queue         | Service Bus   | Basic tier                                          |
| Audio storage | Blob Storage  | Hot tier (auto-cleanup efter 30 dage)               |
| Whisper       | Azure OpenAI  | whisper-1 deployment                                |
| LLM           | Azure OpenAI  | gpt-4.1-nano deployment                             |
| Database      | Turso         | Free/Starter tier                                   |

### Turso-konfiguration

```
# Primær database (hosted)
turso db create transcription-app --group default

# Klient-connection fra Azure Container Apps
DATABASE_URL=libsql://transcription-app-{org}.turso.io
DATABASE_AUTH_TOKEN=<token>
```

### CI/CD

```
Frontend:  GitHub → Netlify (auto-deploy on push)
Backend:   GitHub → Azure Container Registry → Container Apps
           (via GitHub Actions)
```

### Blob Storage lifecycle

```json
{
  "rules": [{
    "name": "cleanup-old-audio",
    "type": "Lifecycle",
    "definition": {
      "filters": { "blobTypes": ["blockBlob"], "prefixMatch": ["audio-chunks/"] },
      "actions": { "baseBlob": { "delete": { "daysAfterModificationGreaterThan": 30 } } }
    }
  }]
}
```

-----

## 9. Omkostningsestimat

### Per 2-timers session

| Komponent                   | Beregning                     | Pris       |
|-----------------------------|-------------------------------|------------|
| Whisper API                 | 120 min × $0.006/min          | ~$0.72     |
| GPT-4.1 Nano (renskrivning) | ~15K tokens input + 5K output | ~$0.01     |
| Blob Storage                | 120 MB, 30 dage               | ~$0.003    |
| Service Bus                 | 24 beskeder                   | ~$0.00     |
| Container Apps              | ~15 min compute (processing)  | ~$0.02     |
| **Total per session**       |                               | **~$0.75** |

### Månedlig (estimat: 40 sessioner)

|                                | Pris         |
|--------------------------------|--------------|
| Variabelt (40 sessioner)       | ~$30         |
| Turso (Starter)                | $0–$29       |
| Netlify (Pro, hvis nødvendigt) | $19          |
| Auth (Clerk, <5 users)         | $0           |
| **Total/måned**                | **~$50–$80** |

-----

## 10. Eksport (V1)

| Format                | Beskrivelse                      |
|-----------------------|----------------------------------|
| **Copy to clipboard** | Kopiér memo-tekst direkte        |
| **Markdown download** | `.md`-fil med formatering        |
| **Rå transkription**  | Uformateret tekst med timestamps |

Docx-eksport og integration med andre systemer kan tilføjes i V2.

-----

## 11. Faseopdeling

### V1 — MVP

- Web-app med mikrofon-capture
- 5-min chunking + kø-baseret transkribering
- Whisper (dansk) + GPT-4.1 Nano renskrivning
- Fuld transkription + opsummerings-mode
- Multi-user med Clerk auth
- Session-historik i Turso
- Markdown-eksport + clipboard

### V2 — Udvidelser

- Systemlyd-capture (getDisplayMedia)
- Speaker diarization via Azure AI Speech
- Realtime status via WebSocket/SSE
- Electron desktop-app med native audio capture
- Docx-eksport
- Tilpassede LLM prompts per bruger

### V3 — Nice-to-have

- Live preview (streaming transkription under optagelse)
- Søgning på tværs af sessioner
- Teams/Outlook-integration
- Automatisk speaker-identifikation (navngivning)
- Multi-sprog detection

-----

## 12. Åbne spørgsmål

1. **Eksportformat** — Er markdown + clipboard nok for V1, eller er docx et must?
1. **Retention policy** — 30 dages auto-sletning af audio OK? Transkriptioner beholdes permanent?
1. **Brugerstyring** — Skal der være teams/organisations-koncept, eller er det flat multi-user?
1. **Concurrent sessions** — Kan én bruger have flere aktive sessioner samtidig?
1. **Whisper model-størrelse** — Azure OpenAI Whisper er pt. kun `whisper-1`. Er kvaliteten tilstrækkelig for dansk, eller skal vi benchmarke?
