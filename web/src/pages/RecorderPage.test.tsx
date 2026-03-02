import { render, screen } from '@testing-library/react';
import { describe, test, expect, vi, beforeEach } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { useRecorderStore } from '../stores/recorder';

vi.mock('../api/client', () => ({
  api: {
    sessions: { create: vi.fn() },
    chunks: { upload: vi.fn() },
    memos: { finalize: vi.fn() },
  },
  setTokenProvider: vi.fn(),
}));

vi.mock('../services/audio', () => ({
  AudioCaptureService: vi.fn().mockImplementation(function() {
    return {
      getStream: vi.fn().mockResolvedValue({ getTracks: () => [] }),
      stop: vi.fn(),
    };
  }),
}));

vi.mock('../services/chunk-cache', () => ({
  ChunkCache: vi.fn().mockImplementation(function() {
    return {
      store: vi.fn(),
      markUploaded: vi.fn(),
      getPending: vi.fn().mockResolvedValue([]),
    };
  }),
}));

vi.stubGlobal('MediaRecorder', vi.fn().mockImplementation(function() {
  return {
    start: vi.fn(),
    stop: vi.fn(),
    ondataavailable: null,
  };
}));

const mockNavigate = vi.fn();
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return { ...actual, useNavigate: () => mockNavigate };
});

import RecorderPage from './RecorderPage';

function renderPage() {
  return render(
    <MemoryRouter>
      <RecorderPage />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  useRecorderStore.getState().reset();
  vi.clearAllMocks();
});

describe('RecorderPage', () => {
  test('shows audio source and output mode pickers when idle', () => {
    renderPage();
    expect(screen.getByLabelText(/audio source/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/output mode/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /start/i })).toBeInTheDocument();
  });

  test('shows timer and stop button when recording', () => {
    useRecorderStore.setState({
      status: 'recording',
      sessionId: 'sess1',
      elapsedMs: 65000,
    });
    renderPage();
    expect(screen.getByText(/00:01:05/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /stop/i })).toBeInTheDocument();
  });

  test('shows chunk status list', () => {
    useRecorderStore.setState({
      status: 'recording',
      sessionId: 'sess1',
      chunks: [
        { chunkIndex: 0, status: 'uploaded' },
        { chunkIndex: 1, status: 'uploading' },
      ],
    });
    renderPage();
    expect(screen.getByText(/chunk 1/i)).toBeInTheDocument();
    expect(screen.getByText(/chunk 2/i)).toBeInTheDocument();
  });

  test('shows finalize button when stopped', () => {
    useRecorderStore.setState({ status: 'stopped', sessionId: 'sess1' });
    renderPage();
    expect(
      screen.getByRole('button', { name: /finalize/i }),
    ).toBeInTheDocument();
  });
});
