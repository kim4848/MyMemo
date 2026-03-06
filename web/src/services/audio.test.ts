import { describe, test, expect, vi, beforeEach } from 'vitest';
import { AudioCaptureService } from './audio';

// Mock browser APIs
const mockMediaStream = {
  getTracks: vi.fn(() => [{ stop: vi.fn() }]),
  getAudioTracks: vi.fn(() => [{ stop: vi.fn() }]),
  getVideoTracks: vi.fn(() => [{ stop: vi.fn() }]),
};

const mockGetUserMedia = vi.fn().mockResolvedValue(mockMediaStream);
const mockGetDisplayMedia = vi.fn().mockResolvedValue(mockMediaStream);

Object.defineProperty(globalThis.navigator, 'mediaDevices', {
  value: {
    getUserMedia: mockGetUserMedia,
    getDisplayMedia: mockGetDisplayMedia,
  },
  writable: true,
});

// Mock AudioContext
const mockMicSourceConnect = vi.fn();
const mockSysSourceConnect = vi.fn();
const mockMicSource = { connect: mockMicSourceConnect };
const mockSysSource = { connect: mockSysSourceConnect };

const mockDestinationStream = {
  getTracks: vi.fn(() => [{ stop: vi.fn() }]),
  getAudioTracks: vi.fn(() => [{ stop: vi.fn() }]),
  getVideoTracks: vi.fn(() => []),
};
const mockDestination = { stream: mockDestinationStream };

let sourceCallCount = 0;
const mockCreateMediaStreamSource = vi.fn(() => {
  sourceCallCount++;
  // First call = mic, second call = system
  return sourceCallCount % 2 === 1 ? mockMicSource : mockSysSource;
});
const mockCreateMediaStreamDestination = vi.fn(() => mockDestination);
const mockAnalyser = {
  fftSize: 2048,
  smoothingTimeConstant: 0.8,
  frequencyBinCount: 128,
  getByteFrequencyData: vi.fn(),
};
const mockCreateAnalyser = vi.fn(() => mockAnalyser);

vi.stubGlobal(
  'AudioContext',
  vi.fn(function (this: Record<string, unknown>) {
    this.createMediaStreamSource = mockCreateMediaStreamSource;
    this.createMediaStreamDestination = mockCreateMediaStreamDestination;
    this.createAnalyser = mockCreateAnalyser;
    this.close = vi.fn();
  }),
);

beforeEach(() => {
  vi.clearAllMocks();
  sourceCallCount = 0;
});

describe('AudioCaptureService', () => {
  test('getStream with "microphone" calls getUserMedia', async () => {
    const service = new AudioCaptureService();
    const stream = await service.getStream('microphone');

    expect(mockGetUserMedia).toHaveBeenCalledWith({ audio: true });
    expect(stream).toBe(mockMediaStream);
  });

  test('getStream with "system" calls getDisplayMedia', async () => {
    const service = new AudioCaptureService();
    const stream = await service.getStream('system');

    expect(mockGetDisplayMedia).toHaveBeenCalledWith({
      audio: true,
      video: true,
    });
    expect(stream).toBe(mockMediaStream);
  });

  test('getStream with "both" merges mic and system into one stream via AudioContext destination', async () => {
    const service = new AudioCaptureService();
    const stream = await service.getStream('both');

    // Both input APIs must be called
    expect(mockGetUserMedia).toHaveBeenCalledWith({ audio: true });
    expect(mockGetDisplayMedia).toHaveBeenCalledWith({
      audio: true,
      video: true,
    });

    // Two MediaStreamSource nodes created (mic + system)
    expect(mockCreateMediaStreamSource).toHaveBeenCalledTimes(2);

    // A destination node must be created for mixing
    expect(mockCreateMediaStreamDestination).toHaveBeenCalledTimes(1);

    // Mic source connected to both its analyser and the destination
    expect(mockMicSourceConnect).toHaveBeenCalledTimes(2);
    expect(mockMicSourceConnect).toHaveBeenCalledWith(mockAnalyser); // analyser
    expect(mockMicSourceConnect).toHaveBeenCalledWith(mockDestination); // destination

    // System source connected to both its analyser and the destination
    expect(mockSysSourceConnect).toHaveBeenCalledTimes(2);
    expect(mockSysSourceConnect).toHaveBeenCalledWith(mockAnalyser); // analyser
    expect(mockSysSourceConnect).toHaveBeenCalledWith(mockDestination); // destination

    // The returned stream must be the destination's merged stream, not a raw input stream
    expect(stream).toBe(mockDestinationStream);
  });

  test('stop releases all tracks', async () => {
    const stopFn = vi.fn();
    const trackedStream = {
      ...mockMediaStream,
      getTracks: vi.fn(() => [{ stop: stopFn }, { stop: stopFn }]),
      getAudioTracks: vi.fn(() => [{ stop: stopFn }]),
      getVideoTracks: vi.fn(() => []),
    };
    mockGetUserMedia.mockResolvedValueOnce(trackedStream);

    const service = new AudioCaptureService();
    await service.getStream('microphone');
    service.stop();

    expect(stopFn).toHaveBeenCalled();
  });
});
