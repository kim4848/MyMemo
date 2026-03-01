import type { AudioSource, OutputMode } from '../types';

interface Props {
  audioSource: AudioSource;
  outputMode: OutputMode;
  onAudioSourceChange: (source: AudioSource) => void;
  onOutputModeChange: (mode: OutputMode) => void;
}

export default function AudioSourcePicker({
  audioSource,
  outputMode,
  onAudioSourceChange,
  onOutputModeChange,
}: Props) {
  return (
    <div className="space-y-4">
      <div>
        <label
          htmlFor="audio-source"
          className="block text-sm font-medium text-gray-400"
        >
          Audio Source
        </label>
        <select
          id="audio-source"
          value={audioSource}
          onChange={(e) => onAudioSourceChange(e.target.value as AudioSource)}
          className="mt-1 block w-full rounded-lg border border-navy-600 bg-navy-700 px-3 py-2.5 text-sm text-gray-200 outline-none focus:border-accent"
        >
          <option value="microphone">Microphone</option>
          <option value="system">System Audio</option>
          <option value="both">Both (Mic + System)</option>
        </select>
      </div>
      <div>
        <label
          htmlFor="output-mode"
          className="block text-sm font-medium text-gray-400"
        >
          Output Mode
        </label>
        <select
          id="output-mode"
          value={outputMode}
          onChange={(e) => onOutputModeChange(e.target.value as OutputMode)}
          className="mt-1 block w-full rounded-lg border border-navy-600 bg-navy-700 px-3 py-2.5 text-sm text-gray-200 outline-none focus:border-accent"
        >
          <option value="full">Full Transcript</option>
          <option value="summary">Summary</option>
          <option value="product-planning">Product Planning</option>
        </select>
      </div>
    </div>
  );
}
