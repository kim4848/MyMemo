import { render, screen } from '@testing-library/react';
import { vi } from 'vitest';

vi.mock('@clerk/clerk-react', () => ({
  ClerkProvider: ({ children }: { children: React.ReactNode }) => children,
  SignedIn: ({ children }: { children: React.ReactNode }) => children,
  SignedOut: () => null,
  UserButton: () => <div data-testid="user-button" />,
  useAuth: () => ({
    isSignedIn: true,
    isLoaded: true,
    getToken: vi.fn().mockResolvedValue('token'),
  }),
}));

vi.mock('./api/client', () => ({
  setTokenProvider: vi.fn(),
  api: {
    sessions: { list: vi.fn().mockResolvedValue([]), create: vi.fn(), delete: vi.fn() },
    chunks: { upload: vi.fn() },
    memos: { finalize: vi.fn(), get: vi.fn() },
  },
}));

vi.mock('./services/chunk-cache', () => ({
  ChunkCache: vi.fn().mockImplementation(function () {
    return { store: vi.fn(), markUploaded: vi.fn(), getPending: vi.fn().mockResolvedValue([]), clearSession: vi.fn() };
  }),
}));

vi.mock('./services/audio', () => ({
  AudioCaptureService: vi.fn().mockImplementation(function () {
    return { getStream: vi.fn().mockResolvedValue({ getTracks: () => [] }), stop: vi.fn() };
  }),
}));

import App from './App';

test('renders app with navigation', () => {
  render(<App />);
  expect(screen.getByText('MyMemo')).toBeInTheDocument();
  expect(screen.getByText('New Recording')).toBeInTheDocument();
});
