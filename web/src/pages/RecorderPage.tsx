import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useRecorderStore } from '../stores/recorder';
import AudioSourcePicker from '../components/AudioSourcePicker';
import RecordingTimer from '../components/RecordingTimer';
import ChunkStatusList from '../components/ChunkStatusList';

export default function RecorderPage() {
  const navigate = useNavigate();
  const {
    status,
    sessionId,
    chunks,
    elapsedMs,
    audioSource,
    outputMode,
    setAudioSource,
    setOutputMode,
    startRecording,
    stopRecording,
    finalize,
  } = useRecorderStore();

  const handleStart = async () => {
    await startRecording();
  };

  const handleStop = () => {
    stopRecording();
  };

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
        <h1 className="text-2xl font-bold text-white">New Recording</h1>
        <div className="mt-1 flex items-center gap-2">
          <span className="inline-block h-1 w-1 rounded-full bg-accent" />
          <span className="h-px w-10 bg-accent" />
        </div>
      </div>

      {status === 'idle' && (
        <div className="space-y-6">
          <div className="rounded-xl border border-navy-700 bg-navy-800 p-6">
            <AudioSourcePicker
              audioSource={audioSource}
              outputMode={outputMode}
              onAudioSourceChange={setAudioSource}
              onOutputModeChange={setOutputMode}
            />
          </div>
          <button
            onClick={handleStart}
            className="flex w-full items-center justify-center gap-2 rounded-xl bg-red-600 px-4 py-4 font-medium text-white transition-colors hover:bg-red-700"
          >
            <svg className="h-5 w-5" fill="currentColor" viewBox="0 0 24 24">
              <circle cx="12" cy="12" r="8" />
            </svg>
            Start Recording
          </button>
        </div>
      )}

      {(status === 'recording' || status === 'stopped') && (
        <div className="space-y-6">
          <div className="flex items-center justify-center rounded-xl border border-navy-700 bg-navy-800 p-8">
            <div className="flex items-center gap-4">
              {status === 'recording' && (
                <span className="h-3 w-3 animate-pulse rounded-full bg-red-500" />
              )}
              <RecordingTimer elapsedMs={elapsedMs} />
            </div>
          </div>

          {status === 'recording' && (
            <button
              onClick={handleStop}
              className="flex w-full items-center justify-center gap-2 rounded-xl bg-navy-700 px-4 py-4 font-medium text-white transition-colors hover:bg-navy-600"
            >
              <svg className="h-5 w-5" fill="currentColor" viewBox="0 0 24 24">
                <rect x="6" y="6" width="12" height="12" rx="1" />
              </svg>
              Stop Recording
            </button>
          )}

          {status === 'stopped' && (
            <button
              onClick={handleFinalize}
              className="flex w-full items-center justify-center gap-2 rounded-xl bg-accent px-4 py-4 font-medium text-white transition-colors hover:bg-accent-hover"
            >
              <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" d="m4.5 12.75 6 6 9-13.5" />
              </svg>
              Finalize &amp; Generate Memo
            </button>
          )}

          <ChunkStatusList chunks={chunks} />
        </div>
      )}

      {status === 'finalizing' && (
        <div className="rounded-xl border border-navy-700 bg-navy-800 p-8 text-center">
          <div className="mx-auto mb-3 h-8 w-8 animate-spin rounded-full border-2 border-gray-600 border-t-accent" />
          <p className="text-gray-400">Finalizing session...</p>
        </div>
      )}

      {error && (
        <div className="mt-4 rounded-xl border border-red-500/20 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          {error}
        </div>
      )}
    </div>
  );
}
