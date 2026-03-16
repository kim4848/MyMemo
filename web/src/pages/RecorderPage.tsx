import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useRecorderStore } from '../stores/recorder';
import AudioSourcePicker from '../components/AudioSourcePicker';
import RecordingTimer from '../components/RecordingTimer';
import AudioLevelIndicator from '../components/AudioLevelIndicator';
import ChunkStatusList from '../components/ChunkStatusList';
import PageHeader from '../components/PageHeader';
import { useAudioLevels } from '../hooks/useAudioLevels';
import { requestNotificationPermission } from '../services/notifications';

function StepIndicator({ status }: { status: string }) {
  const steps = [
    { label: 'Configure', key: 'configure' },
    { label: 'Record', key: 'record' },
    { label: 'Process', key: 'process' },
  ];

  const activeIndex =
    status === 'idle' ? 0
    : status === 'recording' || status === 'paused' ? 1
    : 2; // stopped, finalizing

  return (
    <div className="flex items-center justify-center gap-2 mb-8">
      {steps.map((step, i) => {
        const isCompleted = i < activeIndex;
        const isActive = i === activeIndex;
        return (
          <div key={step.key} className="flex items-center gap-2">
            {i > 0 && (
              <div className={`h-px w-8 ${isCompleted ? 'bg-success' : 'bg-border'}`} />
            )}
            <div className="flex flex-col items-center gap-1">
              <div
                className={`flex h-8 w-8 items-center justify-center rounded-full text-sm font-medium transition-colors ${
                  isCompleted
                    ? 'bg-success text-white'
                    : isActive
                      ? 'bg-accent text-white'
                      : 'bg-bg-hover text-text-muted'
                }`}
              >
                {isCompleted ? (
                  <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" strokeWidth={2} stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" d="m4.5 12.75 6 6 9-13.5" />
                  </svg>
                ) : (
                  i + 1
                )}
              </div>
              <span className={`text-xs ${isActive ? 'text-accent font-medium' : 'text-text-muted'}`}>
                {step.label}
              </span>
            </div>
          </div>
        );
      })}
    </div>
  );
}

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
    <div className="mx-auto max-w-lg animate-[fadeInUp_0.3s_ease-out]">
      <PageHeader title="New Recording" subtitle="Configure your recording settings" />

      <StepIndicator status={status} />

      {status === 'idle' && (
        <div className="space-y-6">
          <div className="rounded-xl border border-border bg-bg-card shadow-sm divide-y divide-border">
            <div className="p-6">
              <AudioSourcePicker
                audioSource={audioSource}
                outputMode={outputMode}
                onAudioSourceChange={setAudioSource}
                onOutputModeChange={setOutputMode}
              />
            </div>
            <div className="p-6">
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
            <div className="p-6">
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
          </div>
          {storeError && (
            <div className="rounded-xl border border-warning/30 bg-warning-light px-4 py-3 text-sm text-warning">
              {storeError}
            </div>
          )}
          <div className="flex flex-col items-center mt-8">
            <div className="relative">
              <div className="absolute inset-[-8px] rounded-full bg-danger/20 animate-[pulse-ring_2s_ease-out_infinite]" />
              <button
                onClick={handleStart}
                className="relative h-20 w-20 rounded-full bg-danger text-white shadow-lg hover:scale-105 transition-transform flex items-center justify-center"
                aria-label="Start Recording"
              >
                <svg className="h-8 w-8" fill="currentColor" viewBox="0 0 24 24">
                  <circle cx="12" cy="12" r="8" />
                </svg>
              </button>
            </div>
            <span className="text-sm font-medium text-text-secondary mt-3">Start Recording</span>
          </div>
        </div>
      )}

      {(status === 'recording' || status === 'paused' || status === 'stopped') && (
        <div className="space-y-6">
          <div className="rounded-xl border border-border bg-bg-card p-6 shadow-sm sm:p-8">
            <div className="flex items-center justify-center gap-4">
              {status === 'recording' && (
                <>
                  {[0, 1, 2, 3, 4].map((i) => (
                    <div
                      key={`bar-l-${i}`}
                      className="w-1 rounded-full bg-danger/60 animate-pulse"
                      style={{
                        height: `${12 + (i % 3) * 6}px`,
                        animationDelay: `${i * 0.15}s`,
                      }}
                    />
                  ))}
                </>
              )}
              {status === 'paused' && (
                <span className="h-3 w-3 rounded-full bg-warning" />
              )}
              <RecordingTimer elapsedMs={elapsedMs} />
              {status === 'recording' && (
                <>
                  {[0, 1, 2, 3, 4].map((i) => (
                    <div
                      key={`bar-r-${i}`}
                      className="w-1 rounded-full bg-danger/60 animate-pulse"
                      style={{
                        height: `${12 + ((4 - i) % 3) * 6}px`,
                        animationDelay: `${(4 - i) * 0.15}s`,
                      }}
                    />
                  ))}
                </>
              )}
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
            <div className="flex justify-center gap-6">
              {status === 'recording' ? (
                <div className="flex flex-col items-center">
                  <button
                    onClick={handlePause}
                    className="h-14 w-14 rounded-full border border-border bg-bg-card flex items-center justify-center text-text-primary transition-colors hover:bg-bg-hover"
                    aria-label="Pause"
                  >
                    <svg className="h-6 w-6" fill="currentColor" viewBox="0 0 24 24">
                      <rect x="6" y="5" width="4" height="14" rx="1" />
                      <rect x="14" y="5" width="4" height="14" rx="1" />
                    </svg>
                  </button>
                  <span className="text-xs text-text-muted mt-1">Pause</span>
                </div>
              ) : (
                <div className="flex flex-col items-center">
                  <button
                    onClick={handleResume}
                    className="h-14 w-14 rounded-full bg-accent flex items-center justify-center text-white transition-colors hover:bg-accent-hover"
                    aria-label="Resume"
                  >
                    <svg className="h-6 w-6" fill="currentColor" viewBox="0 0 24 24">
                      <path d="M8 5v14l11-7z" />
                    </svg>
                  </button>
                  <span className="text-xs text-text-muted mt-1">Resume</span>
                </div>
              )}
              <div className="flex flex-col items-center">
                <button
                  onClick={handleStop}
                  className="h-14 w-14 rounded-full border border-danger/30 bg-bg-card flex items-center justify-center text-danger transition-colors hover:bg-danger-light"
                  aria-label="Stop"
                >
                  <svg className="h-6 w-6" fill="currentColor" viewBox="0 0 24 24">
                    <rect x="6" y="6" width="12" height="12" rx="1" />
                  </svg>
                </button>
                <span className="text-xs text-text-muted mt-1">Stop</span>
              </div>
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
