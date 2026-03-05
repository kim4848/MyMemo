import { render, screen } from '@testing-library/react';
import { describe, test, expect, vi, beforeEach } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { useSessionsStore } from '../stores/sessions';
import type { Session } from '../types';

vi.mock('../api/client', () => ({
  api: {
    sessions: {
      list: vi.fn().mockResolvedValue([]),
      delete: vi.fn().mockResolvedValue(undefined),
    },
  },
}));

import DashboardPage from './DashboardPage';

const mockSessions: Session[] = [
  {
    id: 's1',
    userId: 'u1',
    title: null,
    status: 'completed',
    outputMode: 'full',
    audioSource: 'microphone',
    context: null,
    startedAt: '2026-01-15T10:00:00',
    endedAt: '2026-01-15T11:30:00',
    createdAt: '2026-01-15T10:00:00',
    updatedAt: '2026-01-15T11:30:00',
  },
  {
    id: 's2',
    userId: 'u1',
    title: null,
    status: 'processing',
    outputMode: 'summary',
    audioSource: 'both',
    context: null,
    startedAt: '2026-01-16T14:00:00',
    endedAt: null,
    createdAt: '2026-01-16T14:00:00',
    updatedAt: '2026-01-16T14:00:00',
  },
];

function renderPage() {
  return render(
    <MemoryRouter>
      <DashboardPage />
    </MemoryRouter>,
  );
}

const noopFetch = vi.fn();

beforeEach(() => {
  useSessionsStore.setState({
    sessions: [],
    loading: false,
    error: null,
    fetchSessions: noopFetch,
  });
});

describe('DashboardPage', () => {
  test('shows loading state', () => {
    useSessionsStore.setState({ loading: true });
    renderPage();
    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  test('shows empty state when no sessions', () => {
    renderPage();
    expect(screen.getByText(/no sessions/i)).toBeInTheDocument();
  });

  test('renders session cards', () => {
    useSessionsStore.setState({ sessions: mockSessions });
    renderPage();
    expect(screen.getByText(/completed/i)).toBeInTheDocument();
    expect(screen.getByText(/processing/i)).toBeInTheDocument();
  });

  test('shows error state', () => {
    useSessionsStore.setState({ error: 'Network error' });
    renderPage();
    expect(screen.getByText(/network error/i)).toBeInTheDocument();
  });
});
