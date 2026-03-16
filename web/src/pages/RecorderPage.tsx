import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useRecorderStore } from '../stores/recorder';
import AudioSourcePicker from '../components/AudioSourcePicker';
import RecordingTimer from '../components/RecordingTimer';
import AudioLevelIndicator from '../components/AudioLevelIndicator';
import ChunkStatusList from '../components/ChunkStatusList';
import { useAudioLevels } from '../hooks/useAudioLevels';
import { requestNotificationPermission } from '../services/notifications';

export default function RecorderPage() {
  const navigate = useNavigate();
  const {
    status,
    sessionId,
    chunks,
    elapsedMs,
    audioSource,
    outputMode,
    context,
    transcriptionMode,
    error: storeError,
    setAudioSource,
    setOutputMode,
    setTranscriptionMode,
    setContext,
    startRecording,
    pauseRecording,
    resumeRecording,
    stopRecording,
    finalize,
    reset,
  } = useRecorderStore();

  useEffect(() => {
    if (status === 'finalizing') {
      reset();
    }
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const handleStart = async () => {
    requestNotificationPermission();
    await startRecording();
  };

  const handleStop = () => {
    stopRecording();
  };

  const handlePause = () => {
    pauseRecording();
  };

  const handleResume = () => {
    resumeRecording();
  };

  const audioLevels = useAudioLevels(status === 'recording');
  const [error, setError] = useState<string | null>(null);

  const handleFinalize = async () => {
    try {
      setError(null);
      await finalize();
      navigate(`/sessions/${sessionId}`);
    } catch {
      setError('Failed to finalize session. Please try again.');
    }
  };

  return (
    <div className="mx-auto max-w-lg">
      <div className="mb-6">
        <h1 className="font-[family-name:var(--font-heading)] text-2xl font-semibold text-text-primary">New Recording</h1>
      </div>

      {status === 'idle' && (
        <div className="space-y-6">
          <div className="rounded-xl border border-border bg-bg-card p-6 shadow-sm">
            <AudioSourcePicker
              audioSource={audioSource}
              outputMode={outputMode}
              onAudioSourceChange={setAudioSource}
              onOutputModeChange={setOutputMode}
            />
          </div>
          <div className="rounded-xl border border-border bg-bg-card p-6 shadow-sm">
            <label className="flex items-center gap-3 cursor-pointer">
              <div className="relative">
                <input
                  type="checkbox"
                  checked={transcriptionMode === 'speech'}
                  onChange={(e) => setTranscriptionMode(e.target.checked ? 'speech' : 'whisper')}
                  className="peer sr-only"
                />
                <div className="h-6 w-11 rounded-full bg-border-strong peer-checked:bg-accent transition-colors" />
                <div className="absolute left-0.5 top-0.5 h-5 w-5 rounded-full bg-white transition-transform peer-checked:translate-x-5" />
              </div>
              <div>
                <span className="text-sm font-medium text-text-primary">Med taleridentifikation</span>
                <p className="text-xs text-text-muted">Identificerer hvem der taler (bedst til møder med flere deltagere)</p>
              </div>
            </label>
          </div>
          <div className="rounded-xl border border-border bg-bg-card p-6 shadow-sm">
            <label className="mb-2 block text-sm font-medium text-text-secondary">
              Kontekst (valgfrit)
            </label>
            <textarea
              value={context}
              onChange={(e) => setContext(e.target.value)}
              placeholder="F.eks. møde med København, deltagere: Anne, Bjarne. Emne: teknisk overdragelse af x-produkt"
              rows={3}
              className="w-full resize-none rounded-lg border border-border bg-bg-input px-3 py-2.5 text-sm text-text-primary placeholder:text-text-muted outline-none focus:border-accent focus:ring-2 focus:ring-accent/20"
            />
          </div>
          {storeError && (
            <div className="rounded-xl border border-warning/30 bg-warning-light px-4 py-3 text-sm text-warning">
              {storeError}
            </div>
          )}
          <button
            onClick={handleStart}
            className="flex w-full items-center justify-center gap-2 rounded-xl bg-danger px-4 py-4 font-medium text-white transition-colors hover:bg-red-700"
          >
            <svg className="h-5 w-5" fill="currentColor" viewBox="0 0 24 24">
              <circle cx="12" cy="12" r="8" />
            </svg>
            Start Recording
          </button>
        </div>
      )}

      {(status === 'recording' || status === 'paused' || status === 'stopped') && (
        <div className="space-y-6">
          <div className="rounded-xl border border-border bg-bg-card p-6 shadow-sm sm:p-8">
            <div className="flex items-center justify-center">
              <div className="flex items-center gap-4">
                {status === 'recording' && (
                  <span className="h-3 w-3 animate-pulse rounded-full bg-danger" />
                )}
                {status === 'paused' && (
                  <span className="h-3 w-3 rounded-full bg-warning" />
                )}
                <RecordingTimer elapsedMs={elapsedMs} />
              </div>
            </div>
            {status === 'paused' && (
              <p className="mt-2 text-center text-sm text-warning">Paused</p>
            )}
            {status === 'recording' && (
              <div className="mt-4">
                <AudioLevelIndicator levels={audioLevels} audioSource={audioSource} />
              </div>
            )}
          </div>

          {(status === 'recording' || status === 'paused') && (
            <div className="flex gap-3">
              {status === 'recording' ? (
                <button
                  onClick={handlePause}
                  className="flex flex-1 items-center justify-center gap-2 rounded-xl border border-border bg-bg-card px-4 py-4 font-medium text-text-primary transition-colors hover:bg-bg-hover"
                >
                  <svg className="h-5 w-5" fill="currentColor" viewBox="0 0 24 24">
                    <rect x="6" y="5" width="4" height="14" rx="1" />
                    <rect x="14" y="5" width="4" height="14" rx="1" />
                  </svg>
                  Pause
                </button>
              ) : (
                <button
                  onClick={handleResume}
                  className="flex flex-1 items-center justify-center gap-2 rounded-xl bg-accent px-4 py-4 font-medium text-white transition-colors hover:bg-accent-hover"
                >
                  <svg className="h-5 w-5" fill="currentColor" viewBox="0 0 24 24">
                    <path d="M8 5v14l11-7z" />
                  </svg>
                  Resume
                </button>
              )}
              <button
                onClick={handleStop}
                className="flex flex-1 items-center justify-center gap-2 rounded-xl border border-danger/30 bg-bg-card px-4 py-4 font-medium text-danger transition-colors hover:bg-danger-light"
              >
                <svg className="h-5 w-5" fill="currentColor" viewBox="0 0 24 24">
                  <rect x="6" y="6" width="12" height="12" rx="1" />
                </svg>
                Stop
              </button>
            </div>
          )}

          {status === 'stopped' && (() => {
            const hasIncomplete = chunks.some(
              (c) => c.status === 'pending' || c.status === 'uploading',
            );
            const hasFailed = chunks.some((c) => c.status === 'failed');
            const canFinalize = chunks.length > 0 && !hasIncomplete && !hasFailed;

            return (
              <>
                <button
                  onClick={handleFinalize}
                  disabled={!canFinalize}
                  className={`flex w-full items-center justify-center gap-2 rounded-xl px-4 py-4 font-medium text-white transition-colors ${
                    canFinalize
                      ? 'bg-accent hover:bg-accent-hover'
                      : 'cursor-not-allowed bg-border-strong opacity-50'
                  }`}
                >
                  <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" d="m4.5 12.75 6 6 9-13.5" />
                  </svg>
                  Finalize &amp; Generate Memo
                </button>
                {hasIncomplete && (
                  <p className="text-center text-sm text-warning">
                    Waiting for chunks to finish uploading...
                  </p>
                )}
                {hasFailed && (
                  <p className="text-center text-sm text-danger">
                    Some chunks failed to upload. Please retry or start a new recording.
                  </p>
                )}
              </>
            );
          })()}

          <ChunkStatusList chunks={chunks} />
        </div>
      )}

      {status === 'finalizing' && (
        <div className="rounded-xl border border-border bg-bg-card p-8 text-center shadow-sm">
          <div className="mx-auto mb-3 h-8 w-8 animate-spin rounded-full border-2 border-border border-t-accent" />
          <p className="text-text-muted">Finalizing session...</p>
        </div>
      )}

      {error && (
        <div className="mt-4 rounded-xl border border-danger/30 bg-danger-light px-4 py-3 text-sm text-danger">
          {error}
        </div>
      )}
    </div>
  );
}
