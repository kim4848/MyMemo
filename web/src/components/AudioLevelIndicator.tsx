import type { AudioLevels } from '../hooks/useAudioLevels';
import type { AudioSource } from '../types';

interface Props {
  levels: AudioLevels;
  audioSource: AudioSource;
}

const BAR_COUNT = 20;

function LevelBar({ level, label }: { level: number; label: string }) {
  const activeBars = Math.round(level * BAR_COUNT);

  return (
    <div className="flex items-center gap-2">
      <span className="w-12 shrink-0 text-right text-xs text-text-secondary">
        {label}
      </span>
      <div className="flex flex-1 gap-0.5">
        {Array.from({ length: BAR_COUNT }, (_, i) => {
          const isActive = i < activeBars;
          let color = 'bg-border';
          if (isActive) {
            if (i < BAR_COUNT * 0.6) color = 'bg-success';
            else if (i < BAR_COUNT * 0.85) color = 'bg-yellow-400';
            else color = 'bg-danger';
          }
          return (
            <div
              key={i}
              className={`h-3 flex-1 rounded-md transition-all duration-150 ${color}`}
            />
          );
        })}
      </div>
    </div>
  );
}

export default function AudioLevelIndicator({ levels, audioSource }: Props) {
  return (
    <div className="space-y-1.5">
      {(audioSource === 'microphone' || audioSource === 'both') && (
        <LevelBar level={levels.mic} label="Mikrofon" />
      )}
      {(audioSource === 'system' || audioSource === 'both') && (
        <LevelBar level={levels.system} label="System" />
      )}
    </div>
  );
}
