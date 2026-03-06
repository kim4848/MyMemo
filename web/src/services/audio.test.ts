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
const mockConnect = vi.fn();
const mockDestination = { stream: mockMediaStream };
const mockCreateMediaStreamSource = vi.fn(() => ({ connect: mockConnect }));
const mockCreateMediaStreamDestination = vi.fn(() => mockDestination);
const mockAnalyser = {
  fftSize: 2048,
  smoothingTimeConstant: 0.8,
  frequencyBinCount: 128,
  getByteFrequencyData: vi.fn(),
  connect: mockConnect,
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
});

describe('AudioCaptureService', () => {
  test('getStream with "microphone" calls getUserMedia', async () => {
    const service = new AudioCaptureService();
    const stream = await service.getStream('microphone');

    expect(mockGetUserMedia).toHaveBeenCalledWith({ audio: true });
    expect(stream).toBeDefined();
  });

  test('getStream with "system" calls getDisplayMedia', async () => {
    const service = new AudioCaptureService();
    const stream = await service.getStream('system');

    expect(mockGetDisplayMedia).toHaveBeenCalledWith({
      audio: true,
      video: true,
    });
    expect(stream).toBeDefined();
  });

  test('getStream with "both" combines mic and system audio', async () => {
    const service = new AudioCaptureService();
    const stream = await service.getStream('both');

    expect(mockGetUserMedia).toHaveBeenCalled();
    expect(mockGetDisplayMedia).toHaveBeenCalled();
    expect(mockCreateMediaStreamSource).toHaveBeenCalledTimes(2);
    expect(stream).toBeDefined();
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
