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

  const handleFinalize = async () => {
    await finalize();
    navigate(`/sessions/${sessionId}`);
  };

  return (
    <div className="mx-auto max-w-lg space-y-6">
      <h1 className="text-lg font-semibold text-gray-900">New Recording</h1>

      {status === 'idle' && (
        <>
          <AudioSourcePicker
            audioSource={audioSource}
            outputMode={outputMode}
            onAudioSourceChange={setAudioSource}
            onOutputModeChange={setOutputMode}
          />
          <button
            onClick={handleStart}
            className="w-full rounded-lg bg-red-600 px-4 py-3 font-medium text-white hover:bg-red-700"
          >
            Start Recording
          </button>
        </>
      )}

      {(status === 'recording' || status === 'stopped') && (
        <div className="space-y-4">
          <div className="flex items-center gap-4">
            {status === 'recording' && (
              <span className="h-3 w-3 animate-pulse rounded-full bg-red-500" />
            )}
            <RecordingTimer elapsedMs={elapsedMs} />
          </div>

          {status === 'recording' && (
            <button
              onClick={handleStop}
              className="w-full rounded-lg bg-gray-800 px-4 py-3 font-medium text-white hover:bg-gray-900"
            >
              Stop Recording
            </button>
          )}

          {status === 'stopped' && (
            <button
              onClick={handleFinalize}
              className="w-full rounded-lg bg-blue-600 px-4 py-3 font-medium text-white hover:bg-blue-700"
            >
              Finalize & Generate Memo
            </button>
          )}

          <ChunkStatusList chunks={chunks} />
        </div>
      )}

      {status === 'finalizing' && (
        <div className="py-8 text-center text-gray-500">
          Finalizing session...
        </div>
      )}
    </div>
  );
}
