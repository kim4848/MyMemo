IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'users')
CREATE TABLE users (
    id          NVARCHAR(32) NOT NULL PRIMARY KEY,
    email       NVARCHAR(255) NOT NULL UNIQUE,
    name        NVARCHAR(255) NOT NULL,
    clerk_id    NVARCHAR(255) NOT NULL UNIQUE,
    created_at  NVARCHAR(30) NOT NULL DEFAULT CONVERT(NVARCHAR(30), GETUTCDATE(), 120),
    updated_at  NVARCHAR(30) NOT NULL DEFAULT CONVERT(NVARCHAR(30), GETUTCDATE(), 120)
);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'sessions')
CREATE TABLE sessions (
    id                 NVARCHAR(32) NOT NULL PRIMARY KEY,
    user_id            NVARCHAR(32) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    title              NVARCHAR(255),
    status             NVARCHAR(32) NOT NULL DEFAULT 'recording',
    output_mode        NVARCHAR(32) NOT NULL DEFAULT 'full',
    audio_source       NVARCHAR(32) NOT NULL DEFAULT 'microphone',
    context            NVARCHAR(MAX),
    transcription_mode NVARCHAR(32) NOT NULL DEFAULT 'whisper',
    memo_queued        INT NOT NULL DEFAULT 0,
    started_at         NVARCHAR(30) NOT NULL DEFAULT CONVERT(NVARCHAR(30), GETUTCDATE(), 120),
    ended_at           NVARCHAR(30),
    created_at         NVARCHAR(30) NOT NULL DEFAULT CONVERT(NVARCHAR(30), GETUTCDATE(), 120),
    updated_at         NVARCHAR(30) NOT NULL DEFAULT CONVERT(NVARCHAR(30), GETUTCDATE(), 120)
);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_sessions_user')
CREATE INDEX idx_sessions_user ON sessions(user_id);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'chunks')
CREATE TABLE chunks (
    id            NVARCHAR(32) NOT NULL PRIMARY KEY,
    session_id    NVARCHAR(32) NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    chunk_index   INT NOT NULL,
    blob_path     NVARCHAR(500) NOT NULL,
    duration_sec  INT,
    status        NVARCHAR(32) NOT NULL DEFAULT 'uploaded',
    error_message NVARCHAR(MAX),
    created_at    NVARCHAR(30) NOT NULL DEFAULT CONVERT(NVARCHAR(30), GETUTCDATE(), 120),
    updated_at    NVARCHAR(30) NOT NULL DEFAULT CONVERT(NVARCHAR(30), GETUTCDATE(), 120),
    UNIQUE(session_id, chunk_index)
);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_chunks_session')
CREATE INDEX idx_chunks_session ON chunks(session_id);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'transcriptions')
CREATE TABLE transcriptions (
    id                        NVARCHAR(32) NOT NULL PRIMARY KEY,
    chunk_id                  NVARCHAR(32) NOT NULL UNIQUE REFERENCES chunks(id) ON DELETE CASCADE,
    raw_text                  NVARCHAR(MAX) NOT NULL,
    language                  NVARCHAR(10) DEFAULT 'da',
    confidence                FLOAT,
    word_timestamps           NVARCHAR(MAX),
    transcription_duration_ms BIGINT,
    created_at                NVARCHAR(30) NOT NULL DEFAULT CONVERT(NVARCHAR(30), GETUTCDATE(), 120)
);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'memos')
CREATE TABLE memos (
    id                     NVARCHAR(32) NOT NULL PRIMARY KEY,
    session_id             NVARCHAR(32) NOT NULL UNIQUE REFERENCES sessions(id) ON DELETE CASCADE,
    output_mode            NVARCHAR(32) NOT NULL,
    content                NVARCHAR(MAX) NOT NULL,
    model_used             NVARCHAR(255) NOT NULL,
    prompt_tokens          INT,
    completion_tokens      INT,
    generation_duration_ms BIGINT,
    created_at             NVARCHAR(30) NOT NULL DEFAULT CONVERT(NVARCHAR(30), GETUTCDATE(), 120),
    updated_at             NVARCHAR(30)
);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tags')
CREATE TABLE tags (
    id          NVARCHAR(32) NOT NULL PRIMARY KEY,
    user_id     NVARCHAR(32) NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name        NVARCHAR(255) NOT NULL,
    color       NVARCHAR(32),
    created_at  NVARCHAR(30) NOT NULL DEFAULT CONVERT(NVARCHAR(30), GETUTCDATE(), 120),
    UNIQUE(user_id, name)
);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_tags_user')
CREATE INDEX idx_tags_user ON tags(user_id);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'session_tags')
CREATE TABLE session_tags (
    session_id  NVARCHAR(32) NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    tag_id      NVARCHAR(32) NOT NULL,
    PRIMARY KEY (session_id, tag_id)
);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_session_tags_session')
CREATE INDEX idx_session_tags_session ON session_tags(session_id);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_session_tags_tag')
CREATE INDEX idx_session_tags_tag ON session_tags(tag_id);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'batch_transcription_jobs')
CREATE TABLE batch_transcription_jobs (
    id            NVARCHAR(32) NOT NULL PRIMARY KEY,
    chunk_id      NVARCHAR(32) NOT NULL,
    session_id    NVARCHAR(32) NOT NULL,
    azure_job_id  NVARCHAR(255) NOT NULL,
    status        NVARCHAR(32) NOT NULL DEFAULT 'submitted',
    created_at    NVARCHAR(30) NOT NULL,
    completed_at  NVARCHAR(30)
);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'infographics')
CREATE TABLE infographics (
    id                     NVARCHAR(32) NOT NULL PRIMARY KEY,
    session_id             NVARCHAR(32) NOT NULL UNIQUE REFERENCES sessions(id) ON DELETE CASCADE,
    image_content          NVARCHAR(MAX) NOT NULL,
    model_used             NVARCHAR(255) NOT NULL,
    prompt_tokens          INT,
    completion_tokens      INT,
    generation_duration_ms BIGINT,
    created_at             NVARCHAR(30) NOT NULL DEFAULT CONVERT(NVARCHAR(30), GETUTCDATE(), 120)
);
