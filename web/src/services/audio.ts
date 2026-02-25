import type { AudioSource } from '../types';

export class AudioCaptureService {
  private streams: MediaStream[] = [];
  private audioContext: AudioContext | null = null;

  async getStream(source: AudioSource): Promise<MediaStream> {
    this.stop();

    if (source === 'microphone') {
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: true,
      });
      this.streams = [stream];
      return stream;
    }

    if (source === 'system') {
      const stream = await navigator.mediaDevices.getDisplayMedia({
        audio: true,
        video: true,
      });
      // Discard video tracks
      stream.getVideoTracks().forEach((t) => t.stop());
      this.streams = [stream];
      return stream;
    }

    // "both" — mix mic + system audio
    const micStream = await navigator.mediaDevices.getUserMedia({
      audio: true,
    });
    const sysStream = await navigator.mediaDevices.getDisplayMedia({
      audio: true,
      video: true,
    });
    sysStream.getVideoTracks().forEach((t) => t.stop());

    this.audioContext = new AudioContext();
    const micSource = this.audioContext.createMediaStreamSource(micStream);
    const sysSource = this.audioContext.createMediaStreamSource(sysStream);
    const destination = this.audioContext.createMediaStreamDestination();
    micSource.connect(destination);
    sysSource.connect(destination);

    this.streams = [micStream, sysStream];
    return destination.stream;
  }

  stop() {
    for (const stream of this.streams) {
      stream.getTracks().forEach((t) => t.stop());
    }
    this.streams = [];
    this.audioContext?.close();
    this.audioContext = null;
  }
}
