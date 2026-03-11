import type { AudioSource } from '../types';

export interface AudioAnalysers {
  mic: AnalyserNode | null;
  system: AnalyserNode | null;
}

export class AudioCaptureService {
  private streams: MediaStream[] = [];
  private audioContext: AudioContext | null = null;
  private _analysers: AudioAnalysers = { mic: null, system: null };

  get analysers(): AudioAnalysers {
    return this._analysers;
  }

  async getStream(source: AudioSource): Promise<MediaStream> {
    this.stop();

    if (source === 'microphone') {
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: true,
      });
      this.streams = [stream];

      this.audioContext = new AudioContext();
      const micSource = this.audioContext.createMediaStreamSource(stream);
      this._analysers.mic = this.createAnalyser(this.audioContext);
      micSource.connect(this._analysers.mic);

      return stream;
    }

    if (source === 'system') {
      const stream = await navigator.mediaDevices.getDisplayMedia({
        audio: true,
        video: { displaySurface: 'monitor' },
        systemAudio: 'include',
        selfBrowserSurface: 'exclude',
      } as DisplayMediaStreamOptions);
      // Discard video tracks
      stream.getVideoTracks().forEach((t) => t.stop());
      this.streams = [stream];

      this.audioContext = new AudioContext();
      const sysSource = this.audioContext.createMediaStreamSource(stream);
      this._analysers.system = this.createAnalyser(this.audioContext);
      sysSource.connect(this._analysers.system);

      return stream;
    }

    // "both" — mix mic + system audio
    const micStream = await navigator.mediaDevices.getUserMedia({
      audio: true,
    });
    const sysStream = await navigator.mediaDevices.getDisplayMedia({
      audio: true,
      video: { displaySurface: 'monitor' },
      systemAudio: 'include',
      selfBrowserSurface: 'exclude',
    } as DisplayMediaStreamOptions);
    sysStream.getVideoTracks().forEach((t) => t.stop());

    this.audioContext = new AudioContext();
    const micSource = this.audioContext.createMediaStreamSource(micStream);
    const sysSource = this.audioContext.createMediaStreamSource(sysStream);
    const destination = this.audioContext.createMediaStreamDestination();

    this._analysers.mic = this.createAnalyser(this.audioContext);
    this._analysers.system = this.createAnalyser(this.audioContext);

    micSource.connect(this._analysers.mic);
    micSource.connect(destination);
    sysSource.connect(this._analysers.system);
    sysSource.connect(destination);

    this.streams = [micStream, sysStream];
    return destination.stream;
  }

  stop() {
    for (const stream of this.streams) {
      stream.getTracks().forEach((t) => t.stop());
    }
    this.streams = [];
    this._analysers = { mic: null, system: null };
    this.audioContext?.close();
    this.audioContext = null;
  }

  private createAnalyser(ctx: AudioContext): AnalyserNode {
    const analyser = ctx.createAnalyser();
    analyser.fftSize = 256;
    analyser.smoothingTimeConstant = 0.7;
    return analyser;
  }
}
