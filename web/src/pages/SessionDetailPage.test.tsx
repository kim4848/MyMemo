import { render, screen, waitFor } from '@testing-library/react';
import { describe, test, expect, vi, beforeEach } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router-dom';

vi.mock('../api/client', () => ({
  api: {
    sessions: {
      get: vi.fn(),
    },
    memos: {
      get: vi.fn(),
    },
  },
  ApiError: class ApiError extends Error {
    status: number;
    body: string;
    constructor(status: number, body: string) {
      super(`API error ${status}`);
      this.status = status;
      this.body = body;
    }
  },
  setTokenProvider: vi.fn(),
}));

import { api } from '../api/client';
import type { SessionDetail, Memo } from '../types';
import SessionDetailPage from './SessionDetailPage';

const mockDetail: SessionDetail = {
  session: {
    id: 'sess1',
    userId: 'u1',
    title: null,
    status: 'completed',
    outputMode: 'full',
    audioSource: 'microphone',
    startedAt: '2026-01-15T10:00:00',
    endedAt: '2026-01-15T11:00:00',
    createdAt: '2026-01-15T10:00:00',
    updatedAt: '2026-01-15T11:00:00',
  },
  chunks: [
    {
      id: 'c1',
      sessionId: 'sess1',
      chunkIndex: 0,
      blobPath: 'u1/sess1/0.webm',
      durationSec: 300,
      status: 'transcribed',
      errorMessage: null,
      createdAt: '2026-01-15T10:00:00',
      updatedAt: '2026-01-15T10:05:00',
    },
  ],
};

const mockMemo: Memo = {
  id: 'm1',
  sessionId: 'sess1',
  outputMode: 'full',
  content: 'This is the cleaned transcript.',
  modelUsed: 'gpt-4.1-nano',
  promptTokens: 1000,
  completionTokens: 500,
  createdAt: '2026-01-15T11:01:00',
};

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/sessions/sess1']}>
      <Routes>
        <Route path="/sessions/:id" element={<SessionDetailPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('SessionDetailPage', () => {
  test('shows loading state initially', () => {
    vi.mocked(api.sessions.get).mockReturnValue(new Promise(() => {}));
    renderPage();
    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  test('shows session info and chunks after loading', async () => {
    vi.mocked(api.sessions.get).mockResolvedValue(mockDetail);
    vi.mocked(api.memos.get).mockResolvedValue(mockMemo);

    renderPage();

    await waitFor(() => {
      expect(screen.getByText(/completed/i)).toBeInTheDocument();
    });
    expect(screen.getByText(/chunk 1/i)).toBeInTheDocument();
  });

  test('shows memo content when available', async () => {
    vi.mocked(api.sessions.get).mockResolvedValue(mockDetail);
    vi.mocked(api.memos.get).mockResolvedValue(mockMemo);

    renderPage();

    await waitFor(() => {
      expect(
        screen.getByText(/this is the cleaned transcript/i),
      ).toBeInTheDocument();
    });
  });

  test('shows processing message when memo not ready', async () => {
    vi.mocked(api.sessions.get).mockResolvedValue({
      ...mockDetail,
      session: { ...mockDetail.session, status: 'processing' },
    });
    vi.mocked(api.memos.get).mockRejectedValue({ status: 404 });

    renderPage();

    await waitFor(() => {
      expect(screen.getByText(/generating memo/i)).toBeInTheDocument();
    });
  });
});
