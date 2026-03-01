CREATE TABLE IF NOT EXISTS users (
    id          TEXT PRIMARY KEY DEFAULT (lower(hex(randomblob(16)))),
    email       TEXT NOT NULL UNIQUE,
    name        TEXT NOT NULL,
    clerk_id    TEXT NOT NULL UNIQUE,
    created_at  TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at  TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS sessions (
    id           TEXT PRIMARY KEY DEFAULT (lower(hex(randomblob(16)))),
    user_id      TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    title        TEXT,
    status       TEXT NOT NULL DEFAULT 'recording',
    output_mode  TEXT NOT NULL DEFAULT 'full',
    audio_source TEXT NOT NULL DEFAULT 'microphone',
    memo_queued  INTEGER NOT NULL DEFAULT 0,
    started_at   TEXT NOT NULL DEFAULT (datetime('now')),
    ended_at     TEXT,
    created_at   TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at   TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS idx_sessions_user ON sessions(user_id);

CREATE TABLE IF NOT EXISTS chunks (
    id            TEXT PRIMARY KEY DEFAULT (lower(hex(randomblob(16)))),
    session_id    TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    chunk_index   INTEGER NOT NULL,
    blob_path     TEXT NOT NULL,
    duration_sec  INTEGER,
    status        TEXT NOT NULL DEFAULT 'uploaded',
    error_message TEXT,
    created_at    TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at    TEXT NOT NULL DEFAULT (datetime('now')),
    UNIQUE(session_id, chunk_index)
);

CREATE INDEX IF NOT EXISTS idx_chunks_session ON chunks(session_id);

CREATE TABLE IF NOT EXISTS transcriptions (
    id              TEXT PRIMARY KEY DEFAULT (lower(hex(randomblob(16)))),
    chunk_id                  TEXT NOT NULL UNIQUE REFERENCES chunks(id) ON DELETE CASCADE,
    raw_text                  TEXT NOT NULL,
    language                  TEXT DEFAULT 'da',
    confidence                REAL,
    word_timestamps           TEXT,
    transcription_duration_ms INTEGER,
    created_at                TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS memos (
    id                TEXT PRIMARY KEY DEFAULT (lower(hex(randomblob(16)))),
    session_id            TEXT NOT NULL UNIQUE REFERENCES sessions(id) ON DELETE CASCADE,
    output_mode           TEXT NOT NULL,
    content               TEXT NOT NULL,
    model_used            TEXT NOT NULL,
    prompt_tokens         INTEGER,
    completion_tokens     INTEGER,
    generation_duration_ms INTEGER,
    created_at            TEXT NOT NULL DEFAULT (datetime('now'))
);
