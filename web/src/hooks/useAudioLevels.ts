import { useEffect, useRef, useState } from 'react';
import { getAudioService } from '../stores/recorder';

export interface AudioLevels {
  mic: number;
  system: number;
}

function readLevel(analyser: AnalyserNode | null, buffer: Uint8Array<ArrayBuffer>): number {
  if (!analyser) return 0;
  analyser.getByteFrequencyData(buffer);
  let sum = 0;
  for (let i = 0; i < buffer.length; i++) {
    sum += buffer[i];
  }
  return sum / (buffer.length * 255);
}

export function useAudioLevels(active: boolean): AudioLevels {
  const [levels, setLevels] = useState<AudioLevels>({ mic: 0, system: 0 });
  const rafRef = useRef<number>(0);
  const bufferRef = useRef<Uint8Array<ArrayBuffer> | null>(null);

  useEffect(() => {
    if (!active) {
      setLevels({ mic: 0, system: 0 });
      return;
    }

    function tick() {
      const service = getAudioService();
      if (!service) {
        rafRef.current = requestAnimationFrame(tick);
        return;
      }

      const analysers = service.analysers;
      const size = analysers.mic?.frequencyBinCount ?? analysers.system?.frequencyBinCount ?? 128;
      if (!bufferRef.current || bufferRef.current.length !== size) {
        bufferRef.current = new Uint8Array(size);
      }

      const mic = readLevel(analysers.mic, bufferRef.current);
      const system = readLevel(analysers.system, bufferRef.current);
      setLevels({ mic, system });

      rafRef.current = requestAnimationFrame(tick);
    }

    rafRef.current = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(rafRef.current);
  }, [active]);

  return levels;
}
