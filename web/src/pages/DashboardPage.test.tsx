import { render, screen } from '@testing-library/react';
import { describe, test, expect, vi, beforeEach } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { useSessionsStore } from '../stores/sessions';
import type { SessionWithTags } from '../types';

vi.mock('../api/client', () => ({
  api: {
    sessions: {
      list: vi.fn().mockResolvedValue([]),
      delete: vi.fn().mockResolvedValue(undefined),
    },
    tags: {
      list: vi.fn().mockResolvedValue([]),
    },
  },
}));

import DashboardPage from './DashboardPage';

const mockSessions: SessionWithTags[] = [
  {
    id: 's1',
    userId: 'u1',
    title: null,
    status: 'completed',
    outputMode: 'full',
    audioSource: 'microphone',
    transcriptionMode: 'whisper',
    context: null,
    startedAt: '2026-01-15T10:00:00',
    endedAt: '2026-01-15T11:30:00',
    createdAt: '2026-01-15T10:00:00',
    updatedAt: '2026-01-15T11:30:00',
    tags: [],
  },
  {
    id: 's2',
    userId: 'u1',
    title: null,
    status: 'processing',
    outputMode: 'summary',
    audioSource: 'both',
    transcriptionMode: 'whisper',
    context: null,
    startedAt: '2026-01-16T14:00:00',
    endedAt: null,
    createdAt: '2026-01-16T14:00:00',
    updatedAt: '2026-01-16T14:00:00',
    tags: [],
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
    tags: [],
    loading: false,
    error: null,
    selectedTagIds: [],
    fetchSessions: noopFetch,
    fetchTags: vi.fn(),
  });
});

describe('DashboardPage', () => {
  test('shows loading state', () => {
    useSessionsStore.setState({ loading: true });
    renderPage();
    // Loading state now shows skeleton cards + "Sessions" heading
    expect(screen.getByText('Sessions')).toBeInTheDocument();
  });

  test('shows empty state when no sessions', () => {
    renderPage();
    expect(screen.getByText(/no recordings yet/i)).toBeInTheDocument();
  });

  test('renders session cards', () => {
    useSessionsStore.setState({ sessions: mockSessions });
    renderPage();
    expect(screen.getAllByText(/completed/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/processing/i).length).toBeGreaterThan(0);
  });

  test('shows error state', () => {
    useSessionsStore.setState({ error: 'Network error' });
    renderPage();
    expect(screen.getByText(/network error/i)).toBeInTheDocument();
  });
});
