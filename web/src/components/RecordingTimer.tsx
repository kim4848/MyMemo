interface Props {
  elapsedMs: number;
}

export default function RecordingTimer({ elapsedMs }: Props) {
  const totalSec = Math.floor(elapsedMs / 1000);
  const hours = String(Math.floor(totalSec / 3600)).padStart(2, '0');
  const minutes = String(Math.floor((totalSec % 3600) / 60)).padStart(2, '0');
  const seconds = String(totalSec % 60).padStart(2, '0');

  return (
    <span className="font-mono text-4xl font-light tracking-wider text-text-primary tabular-nums">
      {hours}:{minutes}:{seconds}
    </span>
  );
}
