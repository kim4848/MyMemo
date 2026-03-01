# Web Frontend Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build the React SPA frontend — audio recording with 5-min chunking, session management, and memo viewing — connecting to the existing .NET backend API.

**Architecture:** Vite-bundled React SPA. Clerk handles authentication (JWT passed to API via Bearer token). Audio captured via MediaRecorder with 5-minute WebM/Opus chunks. Chunks cached in IndexedDB for offline resilience, uploaded to backend API with retry. Zustand manages app state. React Router handles navigation. Tailwind CSS v4 for styling.

**Tech Stack:** React, TypeScript, Vite, Tailwind CSS v4, Zustand, React Router, @clerk/clerk-react, Vitest + React Testing Library, idb (IndexedDB), Netlify

---

## Project Structure

```
web/
├── index.html
├── package.json
├── tsconfig.json
├── tsconfig.app.json
├── tsconfig.node.json
├── vite.config.ts
├── netlify.toml
├── .env.example
├── src/
│   ├── main.tsx
│   ├── App.tsx
│   ├── index.css
│   ├── vite-env.d.ts
│   ├── types.ts
│   ├── api/
│   │   ├── client.ts
│   │   └── client.test.ts
│   ├── stores/
│   │   ├── sessions.ts
│   │   ├── sessions.test.ts
│   │   ├── recorder.ts
│   │   └── recorder.test.ts
│   ├── services/
│   │   ├── audio.ts
│   │   ├── audio.test.ts
│   │   ├── chunk-cache.ts
│   │   └── chunk-cache.test.ts
│   ├── pages/
│   │   ├── LoginPage.tsx
│   │   ├── DashboardPage.tsx
│   │   ├── DashboardPage.test.tsx
│   │   ├── RecorderPage.tsx
│   │   ├── RecorderPage.test.tsx
│   │   ├── SessionDetailPage.tsx
│   │   └── SessionDetailPage.test.tsx
│   ├── components/
│   │   ├── Layout.tsx
│   │   ├── SessionCard.tsx
│   │   ├── ChunkStatusList.tsx
│   │   ├── AudioSourcePicker.tsx
│   │   ├── RecordingTimer.tsx
│   │   └── MemoViewer.tsx
│   └── lib/
│       └── format.ts
├── test-setup.ts
```

## Routes

| Path | Page | Auth Required |
|------|------|---------------|
| `/login` | LoginPage | No |
| `/` | DashboardPage | Yes |
| `/record` | RecorderPage | Yes |
| `/sessions/:id` | SessionDetailPage | Yes |

---

### Task 1: Scaffold Vite + React + TypeScript Project

**Files:**
- Create: `web/` (entire scaffold)
- Modify: `web/package.json` (add scripts)

**Step 1: Scaffold with Vite**

Run from repo root:
```bash
cd web && npm create vite@latest . -- --template react-ts
```

If it asks to overwrite existing files (README.md), say yes.

**Step 2: Install dependencies**

```bash
cd web && npm install
```

**Step 3: Clean up default files**

Delete these generated files:
- `src/App.css`
- `src/assets/react.svg`
- `public/vite.svg`

Replace `src/App.tsx` with:

```tsx
export default function App() {
  return <div>MyMemo</div>;
}
```

Replace `src/index.css` with empty file (will be configured in Task 2).

Replace `src/main.tsx` with:

```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import './index.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
```

**Step 4: Verify dev server starts**

```bash
cd web && npm run dev
```

Expected: Dev server starts on localhost, shows "MyMemo" text.
Kill the server after verifying.

**Step 5: Commit**

```bash
git add web/
git commit -m "feat(web): scaffold Vite + React + TypeScript project"
```

---

### Task 2: Configure Tailwind CSS v4

**Files:**
- Modify: `web/vite.config.ts`
- Modify: `web/src/index.css`

**Step 1: Install Tailwind CSS v4 with Vite plugin**

```bash
cd web && npm install tailwindcss @tailwindcss/vite
```

**Step 2: Add Vite plugin**

`web/vite.config.ts`:

```ts
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  plugins: [react(), tailwindcss()],
});
```

**Step 3: Import Tailwind in CSS**

`web/src/index.css`:

```css
@import "tailwindcss";
```

**Step 4: Verify Tailwind works**

Temporarily update `src/App.tsx`:

```tsx
export default function App() {
  return <div className="text-2xl font-bold text-blue-600 p-4">MyMemo</div>;
}
```

```bash
cd web && npm run dev
```

Expected: "MyMemo" text appears large, bold, blue. Kill server after verifying, then revert App.tsx to just `<div>MyMemo</div>`.

**Step 5: Commit**

```bash
git add web/
git commit -m "feat(web): configure Tailwind CSS v4"
```

---

### Task 3: Configure Testing Infrastructure

**Files:**
- Create: `web/test-setup.ts`
- Modify: `web/vite.config.ts`
- Modify: `web/package.json`
- Create: `web/src/App.test.tsx`

**Step 1: Install testing dependencies**

```bash
cd web && npm install -D vitest @testing-library/react @testing-library/jest-dom @testing-library/user-event jsdom
```

**Step 2: Configure Vitest in vite.config.ts**

`web/vite.config.ts`:

```ts
/// <reference types="vitest" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './test-setup.ts',
    css: false,
  },
});
```

**Step 3: Create test setup file**

`web/test-setup.ts`:

```ts
import '@testing-library/jest-dom/vitest';
```

**Step 4: Add test script to package.json**

Add to `scripts` in `web/package.json`:

```json
"test": "vitest run",
"test:watch": "vitest"
```

**Step 5: Write smoke test**

`web/src/App.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import App from './App';

test('renders app', () => {
  render(<App />);
  expect(screen.getByText('MyMemo')).toBeInTheDocument();
});
```

**Step 6: Run smoke test**

```bash
cd web && npm test
```

Expected: 1 test passes.

**Step 7: Commit**

```bash
git add web/
git commit -m "feat(web): configure Vitest + React Testing Library"
```

---

### Task 4: TypeScript API Types

**Files:**
- Create: `web/src/types.ts`

**Step 1: Define all API model types**

`web/src/types.ts`:

```ts
export type OutputMode = 'full' | 'summary';
export type AudioSource = 'microphone' | 'system' | 'both';
export type SessionStatus = 'recording' | 'processing' | 'completed' | 'failed';
export type ChunkStatus = 'uploaded' | 'queued' | 'transcribing' | 'transcribed' | 'failed';

export interface Session {
  id: string;
  userId: string;
  title: string | null;
  status: SessionStatus;
  outputMode: OutputMode;
  audioSource: AudioSource;
  startedAt: string;
  endedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface Chunk {
  id: string;
  sessionId: string;
  chunkIndex: number;
  blobPath: string;
  durationSec: number | null;
  status: ChunkStatus;
  errorMessage: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface SessionDetail {
  session: Session;
  chunks: Chunk[];
}

export interface Memo {
  id: string;
  sessionId: string;
  outputMode: OutputMode;
  content: string;
  modelUsed: string;
  promptTokens: number | null;
  completionTokens: number | null;
  createdAt: string;
}

export interface CreateSessionRequest {
  outputMode: OutputMode;
  audioSource: AudioSource;
}
```

**Step 2: Commit**

```bash
git add web/src/types.ts
git commit -m "feat(web): add TypeScript API types"
```

---

### Task 5: API Client (TDD)

**Files:**
- Create: `web/src/api/client.ts`
- Create: `web/src/api/client.test.ts`

**Step 1: Write failing tests**

`web/src/api/client.test.ts`:

```ts
import { describe, test, expect, vi, beforeEach } from 'vitest';
import { api, setTokenProvider, ApiError } from './client';

const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

beforeEach(() => {
  mockFetch.mockReset();
  setTokenProvider(() => Promise.resolve('test-token'));
});

describe('api.sessions', () => {
  test('create sends POST with body and returns session', async () => {
    const session = { id: 'abc', status: 'recording' };
    mockFetch.mockResolvedValue({
      ok: true,
      status: 201,
      json: () => Promise.resolve(session),
    });

    const result = await api.sessions.create({
      outputMode: 'full',
      audioSource: 'microphone',
    });

    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/sessions'),
      expect.objectContaining({
        method: 'POST',
        headers: expect.objectContaining({
          Authorization: 'Bearer test-token',
          'Content-Type': 'application/json',
        }),
      }),
    );
    expect(result).toEqual(session);
  });

  test('list sends GET and returns sessions array', async () => {
    const sessions = [{ id: '1' }, { id: '2' }];
    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve(sessions),
    });

    const result = await api.sessions.list();
    expect(result).toEqual(sessions);
  });

  test('get sends GET with id and returns session detail', async () => {
    const detail = { session: { id: '1' }, chunks: [] };
    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve(detail),
    });

    const result = await api.sessions.get('abc');
    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/sessions/abc'),
      expect.any(Object),
    );
    expect(result).toEqual(detail);
  });

  test('delete sends DELETE and returns void', async () => {
    mockFetch.mockResolvedValue({ ok: true, status: 204 });

    await api.sessions.delete('abc');
    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/sessions/abc'),
      expect.objectContaining({ method: 'DELETE' }),
    );
  });
});

describe('api.chunks', () => {
  test('upload sends multipart form data', async () => {
    const chunk = { id: 'c1', chunkIndex: 0, status: 'queued' };
    mockFetch.mockResolvedValue({
      ok: true,
      status: 202,
      json: () => Promise.resolve(chunk),
    });

    const blob = new Blob(['audio'], { type: 'audio/webm' });
    const result = await api.chunks.upload('sess1', blob, 0);

    const [url, options] = mockFetch.mock.calls[0];
    expect(url).toContain('/api/sessions/sess1/chunks');
    expect(options.method).toBe('POST');
    expect(options.body).toBeInstanceOf(FormData);
    expect(result).toEqual(chunk);
  });
});

describe('api.memos', () => {
  test('finalize sends POST', async () => {
    mockFetch.mockResolvedValue({ ok: true, status: 202 });

    await api.memos.finalize('sess1');
    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/sessions/sess1/finalize'),
      expect.objectContaining({ method: 'POST' }),
    );
  });

  test('get returns memo', async () => {
    const memo = { id: 'm1', content: 'Hello' };
    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve(memo),
    });

    const result = await api.memos.get('sess1');
    expect(result).toEqual(memo);
  });
});

describe('error handling', () => {
  test('throws ApiError on non-ok response', async () => {
    mockFetch.mockResolvedValue({
      ok: false,
      status: 404,
      text: () => Promise.resolve('Not found'),
    });

    await expect(api.sessions.list()).rejects.toThrow(ApiError);
    await expect(api.sessions.list()).rejects.toMatchObject({
      status: 404,
    });
  });

  test('includes auth header from token provider', async () => {
    setTokenProvider(() => Promise.resolve('my-jwt'));
    mockFetch.mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve([]),
    });

    await api.sessions.list();
    expect(mockFetch).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer my-jwt',
        }),
      }),
    );
  });
});
```

**Step 2: Run tests to verify they fail**

```bash
cd web && npm test -- src/api/client.test.ts
```

Expected: FAIL — module `./client` not found.

**Step 3: Implement the API client**

`web/src/api/client.ts`:

```ts
import type {
  Session,
  SessionDetail,
  Chunk,
  Memo,
  CreateSessionRequest,
} from '../types';

const API_BASE = import.meta.env.VITE_API_URL ?? '';

type TokenProvider = () => Promise<string | null>;
let tokenProvider: TokenProvider = () => Promise.resolve(null);

export function setTokenProvider(provider: TokenProvider) {
  tokenProvider = provider;
}

export class ApiError extends Error {
  constructor(
    public status: number,
    public body: string,
  ) {
    super(`API error ${status}: ${body}`);
    this.name = 'ApiError';
  }
}

async function request<T>(
  path: string,
  options?: RequestInit,
): Promise<T> {
  const token = await tokenProvider();
  const headers: Record<string, string> = {
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(options?.headers as Record<string, string>),
  };

  const res = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers,
  });

  if (!res.ok) {
    throw new ApiError(res.status, await res.text());
  }

  if (res.status === 204) return undefined as T;
  return res.json();
}

export const api = {
  sessions: {
    create: (body: CreateSessionRequest) =>
      request<Session>('/api/sessions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      }),
    list: () => request<Session[]>('/api/sessions'),
    get: (id: string) => request<SessionDetail>(`/api/sessions/${id}`),
    delete: (id: string) =>
      request<void>(`/api/sessions/${id}`, { method: 'DELETE' }),
  },
  chunks: {
    upload: (sessionId: string, audio: Blob, chunkIndex: number) => {
      const form = new FormData();
      form.append('audio', audio);
      form.append('chunkIndex', chunkIndex.toString());
      return request<Chunk>(`/api/sessions/${sessionId}/chunks`, {
        method: 'POST',
        body: form,
      });
    },
  },
  memos: {
    finalize: (sessionId: string) =>
      request<void>(`/api/sessions/${sessionId}/finalize`, {
        method: 'POST',
      }),
    get: (sessionId: string) =>
      request<Memo>(`/api/sessions/${sessionId}/memo`),
  },
};
```

**Step 4: Run tests to verify they pass**

```bash
cd web && npm test -- src/api/client.test.ts
```

Expected: All tests pass.

**Step 5: Commit**

```bash
git add web/src/api/
git commit -m "feat(web): add typed API client with auth token support"
```

---

### Task 6: Clerk Authentication + Login Page

**Files:**
- Modify: `web/src/main.tsx`
- Create: `web/src/pages/LoginPage.tsx`
- Modify: `web/.env.example`

**Step 1: Install Clerk React SDK**

```bash
cd web && npm install @clerk/clerk-react
```

**Step 2: Create .env.example**

`web/.env.example`:

```env
VITE_CLERK_PUBLISHABLE_KEY=pk_test_...
VITE_API_URL=http://localhost:5000
```

Create a local `.env` (gitignored) with actual Clerk key for development.

**Step 3: Wrap app with ClerkProvider**

`web/src/main.tsx`:

```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { ClerkProvider } from '@clerk/clerk-react';
import App from './App';
import './index.css';

const clerkPubKey = import.meta.env.VITE_CLERK_PUBLISHABLE_KEY;

if (!clerkPubKey) {
  throw new Error('Missing VITE_CLERK_PUBLISHABLE_KEY env variable');
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ClerkProvider publishableKey={clerkPubKey}>
      <App />
    </ClerkProvider>
  </StrictMode>,
);
```

**Step 4: Create Login page**

`web/src/pages/LoginPage.tsx`:

```tsx
import { SignIn } from '@clerk/clerk-react';

export default function LoginPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-50">
      <SignIn routing="hash" />
    </div>
  );
}
```

**Step 5: Commit**

```bash
git add web/
git commit -m "feat(web): add Clerk authentication and login page"
```

---

### Task 7: Routing + App Layout

**Files:**
- Modify: `web/src/App.tsx`
- Create: `web/src/components/Layout.tsx`
- Create: `web/src/pages/DashboardPage.tsx` (placeholder)
- Create: `web/src/pages/RecorderPage.tsx` (placeholder)
- Create: `web/src/pages/SessionDetailPage.tsx` (placeholder)

**Step 1: Install React Router**

```bash
cd web && npm install react-router-dom
```

**Step 2: Create Layout component**

`web/src/components/Layout.tsx`:

```tsx
import { Link, Outlet, useNavigate } from 'react-router-dom';
import { useAuth, UserButton } from '@clerk/clerk-react';
import { useEffect } from 'react';
import { setTokenProvider } from '../api/client';

export default function Layout() {
  const { isSignedIn, isLoaded, getToken } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (isLoaded && !isSignedIn) {
      navigate('/login');
    }
  }, [isLoaded, isSignedIn, navigate]);

  useEffect(() => {
    setTokenProvider(() => getToken());
  }, [getToken]);

  if (!isLoaded) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <div className="text-gray-500">Loading...</div>
      </div>
    );
  }

  if (!isSignedIn) return null;

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="border-b bg-white">
        <div className="mx-auto flex max-w-4xl items-center justify-between px-4 py-3">
          <Link to="/" className="text-lg font-semibold text-gray-900">
            MyMemo
          </Link>
          <div className="flex items-center gap-4">
            <Link
              to="/record"
              className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
            >
              New Recording
            </Link>
            <UserButton />
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-4xl px-4 py-6">
        <Outlet />
      </main>
    </div>
  );
}
```

**Step 3: Create placeholder pages**

`web/src/pages/DashboardPage.tsx`:

```tsx
export default function DashboardPage() {
  return <div>Dashboard</div>;
}
```

`web/src/pages/RecorderPage.tsx`:

```tsx
export default function RecorderPage() {
  return <div>Recorder</div>;
}
```

`web/src/pages/SessionDetailPage.tsx`:

```tsx
export default function SessionDetailPage() {
  return <div>Session Detail</div>;
}
```

**Step 4: Wire up routes in App.tsx**

`web/src/App.tsx`:

```tsx
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { SignedIn, SignedOut } from '@clerk/clerk-react';
import Layout from './components/Layout';
import LoginPage from './pages/LoginPage';
import DashboardPage from './pages/DashboardPage';
import RecorderPage from './pages/RecorderPage';
import SessionDetailPage from './pages/SessionDetailPage';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route
          path="/login"
          element={
            <>
              <SignedIn>
                <Navigate to="/" replace />
              </SignedIn>
              <SignedOut>
                <LoginPage />
              </SignedOut>
            </>
          }
        />
        <Route element={<Layout />}>
          <Route index element={<DashboardPage />} />
          <Route path="record" element={<RecorderPage />} />
          <Route path="sessions/:id" element={<SessionDetailPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
```

**Step 5: Update App.test.tsx for new structure**

`web/src/App.test.tsx`:

```tsx
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
}));

import App from './App';

test('renders app with navigation', () => {
  render(<App />);
  expect(screen.getByText('MyMemo')).toBeInTheDocument();
  expect(screen.getByText('New Recording')).toBeInTheDocument();
});
```

**Step 6: Run tests**

```bash
cd web && npm test
```

Expected: All tests pass.

**Step 7: Commit**

```bash
git add web/
git commit -m "feat(web): add routing, layout, and placeholder pages"
```

---

### Task 8: Session Store (TDD)

**Files:**
- Create: `web/src/stores/sessions.ts`
- Create: `web/src/stores/sessions.test.ts`

**Step 1: Install Zustand**

```bash
cd web && npm install zustand
```

**Step 2: Write failing tests**

`web/src/stores/sessions.test.ts`:

```ts
import { describe, test, expect, vi, beforeEach } from 'vitest';
import { useSessionsStore } from './sessions';

vi.mock('../api/client', () => ({
  api: {
    sessions: {
      list: vi.fn(),
      create: vi.fn(),
      delete: vi.fn(),
    },
  },
}));

import { api } from '../api/client';

const mockSession = {
  id: 's1',
  userId: 'u1',
  title: null,
  status: 'recording' as const,
  outputMode: 'full' as const,
  audioSource: 'microphone' as const,
  startedAt: '2026-01-01T00:00:00',
  endedAt: null,
  createdAt: '2026-01-01T00:00:00',
  updatedAt: '2026-01-01T00:00:00',
};

beforeEach(() => {
  useSessionsStore.setState({
    sessions: [],
    loading: false,
    error: null,
  });
  vi.clearAllMocks();
});

describe('fetchSessions', () => {
  test('sets loading true, fetches, then sets sessions', async () => {
    vi.mocked(api.sessions.list).mockResolvedValue([mockSession]);

    const promise = useSessionsStore.getState().fetchSessions();
    expect(useSessionsStore.getState().loading).toBe(true);

    await promise;
    expect(useSessionsStore.getState().loading).toBe(false);
    expect(useSessionsStore.getState().sessions).toEqual([mockSession]);
    expect(useSessionsStore.getState().error).toBeNull();
  });

  test('sets error on failure', async () => {
    vi.mocked(api.sessions.list).mockRejectedValue(new Error('fail'));

    await useSessionsStore.getState().fetchSessions();
    expect(useSessionsStore.getState().loading).toBe(false);
    expect(useSessionsStore.getState().error).toBe('fail');
    expect(useSessionsStore.getState().sessions).toEqual([]);
  });
});

describe('createSession', () => {
  test('creates session and adds to store', async () => {
    vi.mocked(api.sessions.create).mockResolvedValue(mockSession);

    const result = await useSessionsStore.getState().createSession({
      outputMode: 'full',
      audioSource: 'microphone',
    });

    expect(result).toEqual(mockSession);
    expect(useSessionsStore.getState().sessions).toContainEqual(mockSession);
  });
});

describe('deleteSession', () => {
  test('removes session from store', async () => {
    useSessionsStore.setState({ sessions: [mockSession] });
    vi.mocked(api.sessions.delete).mockResolvedValue(undefined);

    await useSessionsStore.getState().deleteSession('s1');
    expect(useSessionsStore.getState().sessions).toEqual([]);
  });
});
```

**Step 3: Run tests to verify they fail**

```bash
cd web && npm test -- src/stores/sessions.test.ts
```

Expected: FAIL — module `./sessions` not found.

**Step 4: Implement the session store**

`web/src/stores/sessions.ts`:

```ts
import { create } from 'zustand';
import { api } from '../api/client';
import type { Session, CreateSessionRequest } from '../types';

interface SessionsState {
  sessions: Session[];
  loading: boolean;
  error: string | null;
  fetchSessions: () => Promise<void>;
  createSession: (req: CreateSessionRequest) => Promise<Session>;
  deleteSession: (id: string) => Promise<void>;
}

export const useSessionsStore = create<SessionsState>((set, get) => ({
  sessions: [],
  loading: false,
  error: null,

  fetchSessions: async () => {
    set({ loading: true, error: null });
    try {
      const sessions = await api.sessions.list();
      set({ sessions, loading: false });
    } catch (e) {
      set({ error: (e as Error).message, loading: false });
    }
  },

  createSession: async (req) => {
    const session = await api.sessions.create(req);
    set({ sessions: [session, ...get().sessions] });
    return session;
  },

  deleteSession: async (id) => {
    await api.sessions.delete(id);
    set({ sessions: get().sessions.filter((s) => s.id !== id) });
  },
}));
```

**Step 5: Run tests to verify they pass**

```bash
cd web && npm test -- src/stores/sessions.test.ts
```

Expected: All tests pass.

**Step 6: Commit**

```bash
git add web/src/stores/
git commit -m "feat(web): add Zustand session store with CRUD actions"
```

---

### Task 9: Dashboard Page (TDD)

**Files:**
- Create: `web/src/components/SessionCard.tsx`
- Modify: `web/src/pages/DashboardPage.tsx`
- Create: `web/src/pages/DashboardPage.test.tsx`
- Create: `web/src/lib/format.ts`

**Step 1: Create format helpers**

`web/src/lib/format.ts`:

```ts
export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleDateString('da-DK', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function formatDuration(startIso: string, endIso: string | null): string {
  const start = new Date(startIso).getTime();
  const end = endIso ? new Date(endIso).getTime() : Date.now();
  const totalSec = Math.floor((end - start) / 1000);
  const hours = Math.floor(totalSec / 3600);
  const minutes = Math.floor((totalSec % 3600) / 60);
  if (hours > 0) return `${hours}h ${minutes}m`;
  return `${minutes}m`;
}
```

**Step 2: Write failing test for DashboardPage**

`web/src/pages/DashboardPage.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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

beforeEach(() => {
  useSessionsStore.setState({
    sessions: [],
    loading: false,
    error: null,
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
```

**Step 3: Run tests to verify they fail**

```bash
cd web && npm test -- src/pages/DashboardPage.test.tsx
```

Expected: FAIL — DashboardPage doesn't render expected elements.

**Step 4: Create SessionCard component**

`web/src/components/SessionCard.tsx`:

```tsx
import { Link } from 'react-router-dom';
import type { Session } from '../types';
import { formatDateTime, formatDuration } from '../lib/format';

const statusStyles: Record<string, string> = {
  recording: 'bg-yellow-100 text-yellow-800',
  processing: 'bg-blue-100 text-blue-800',
  completed: 'bg-green-100 text-green-800',
  failed: 'bg-red-100 text-red-800',
};

interface Props {
  session: Session;
  onDelete: (id: string) => void;
}

export default function SessionCard({ session, onDelete }: Props) {
  return (
    <div className="flex items-center justify-between rounded-lg border bg-white p-4">
      <Link to={`/sessions/${session.id}`} className="flex-1">
        <div className="flex items-center gap-3">
          <span
            className={`rounded-full px-2 py-0.5 text-xs font-medium ${statusStyles[session.status] ?? ''}`}
          >
            {session.status}
          </span>
          <span className="text-sm text-gray-500">
            {formatDateTime(session.createdAt)}
          </span>
          <span className="text-sm text-gray-400">
            {formatDuration(session.startedAt, session.endedAt)}
          </span>
          <span className="text-xs text-gray-400">
            {session.outputMode} · {session.audioSource}
          </span>
        </div>
      </Link>
      <button
        onClick={() => onDelete(session.id)}
        className="ml-4 text-sm text-red-500 hover:text-red-700"
      >
        Delete
      </button>
    </div>
  );
}
```

**Step 5: Implement DashboardPage**

`web/src/pages/DashboardPage.tsx`:

```tsx
import { useEffect } from 'react';
import { useSessionsStore } from '../stores/sessions';
import SessionCard from '../components/SessionCard';

export default function DashboardPage() {
  const { sessions, loading, error, fetchSessions, deleteSession } =
    useSessionsStore();

  useEffect(() => {
    fetchSessions();
  }, [fetchSessions]);

  if (loading) {
    return <div className="py-8 text-center text-gray-500">Loading...</div>;
  }

  if (error) {
    return (
      <div className="py-8 text-center text-red-500">{error}</div>
    );
  }

  if (sessions.length === 0) {
    return (
      <div className="py-12 text-center">
        <p className="text-gray-500">No sessions yet</p>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      <h1 className="text-lg font-semibold text-gray-900">Sessions</h1>
      {sessions.map((session) => (
        <SessionCard
          key={session.id}
          session={session}
          onDelete={deleteSession}
        />
      ))}
    </div>
  );
}
```

**Step 6: Run tests to verify they pass**

```bash
cd web && npm test -- src/pages/DashboardPage.test.tsx
```

Expected: All tests pass.

**Step 7: Commit**

```bash
git add web/src/pages/DashboardPage.tsx web/src/pages/DashboardPage.test.tsx web/src/components/SessionCard.tsx web/src/lib/format.ts
git commit -m "feat(web): add dashboard page with session list"
```

---

### Task 10: Audio Capture Service (TDD)

**Files:**
- Create: `web/src/services/audio.ts`
- Create: `web/src/services/audio.test.ts`

**Step 1: Write failing tests**

`web/src/services/audio.test.ts`:

```ts
import { describe, test, expect, vi, beforeEach } from 'vitest';
import { AudioCaptureService } from './audio';
import type { AudioSource } from '../types';

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

vi.stubGlobal(
  'AudioContext',
  vi.fn(() => ({
    createMediaStreamSource: mockCreateMediaStreamSource,
    createMediaStreamDestination: mockCreateMediaStreamDestination,
    close: vi.fn(),
  })),
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
```

**Step 2: Run tests to verify they fail**

```bash
cd web && npm test -- src/services/audio.test.ts
```

Expected: FAIL — module `./audio` not found.

**Step 3: Implement the audio capture service**

`web/src/services/audio.ts`:

```ts
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
```

**Step 4: Run tests to verify they pass**

```bash
cd web && npm test -- src/services/audio.test.ts
```

Expected: All tests pass.

**Step 5: Commit**

```bash
git add web/src/services/audio.ts web/src/services/audio.test.ts
git commit -m "feat(web): add audio capture service with mic/system/both support"
```

---

### Task 11: IndexedDB Chunk Cache (TDD)

**Files:**
- Create: `web/src/services/chunk-cache.ts`
- Create: `web/src/services/chunk-cache.test.ts`

**Step 1: Install dependencies**

```bash
cd web && npm install idb && npm install -D fake-indexeddb
```

**Step 2: Write failing tests**

`web/src/services/chunk-cache.test.ts`:

```ts
import 'fake-indexeddb/auto';
import { describe, test, expect, beforeEach } from 'vitest';
import { ChunkCache } from './chunk-cache';

let cache: ChunkCache;

beforeEach(async () => {
  // Use a unique DB name per test to avoid state leaking
  cache = new ChunkCache(`test-db-${Math.random()}`);
});

describe('ChunkCache', () => {
  test('stores and retrieves a pending chunk', async () => {
    const blob = new Blob(['audio-data'], { type: 'audio/webm' });
    await cache.store('sess1', 0, blob);

    const pending = await cache.getPending('sess1');
    expect(pending).toHaveLength(1);
    expect(pending[0].sessionId).toBe('sess1');
    expect(pending[0].chunkIndex).toBe(0);
    expect(pending[0].blob.size).toBe(blob.size);
  });

  test('marks chunk as uploaded and removes from pending', async () => {
    const blob = new Blob(['audio-data']);
    await cache.store('sess1', 0, blob);
    await cache.markUploaded('sess1', 0);

    const pending = await cache.getPending('sess1');
    expect(pending).toHaveLength(0);
  });

  test('stores multiple chunks in order', async () => {
    await cache.store('sess1', 0, new Blob(['a']));
    await cache.store('sess1', 1, new Blob(['b']));
    await cache.store('sess1', 2, new Blob(['c']));

    const pending = await cache.getPending('sess1');
    expect(pending).toHaveLength(3);
    expect(pending.map((p) => p.chunkIndex)).toEqual([0, 1, 2]);
  });

  test('clearSession removes all chunks for a session', async () => {
    await cache.store('sess1', 0, new Blob(['a']));
    await cache.store('sess1', 1, new Blob(['b']));
    await cache.store('sess2', 0, new Blob(['c']));

    await cache.clearSession('sess1');

    const s1 = await cache.getPending('sess1');
    const s2 = await cache.getPending('sess2');
    expect(s1).toHaveLength(0);
    expect(s2).toHaveLength(1);
  });
});
```

**Step 3: Run tests to verify they fail**

```bash
cd web && npm test -- src/services/chunk-cache.test.ts
```

Expected: FAIL — module `./chunk-cache` not found.

**Step 4: Implement the chunk cache**

`web/src/services/chunk-cache.ts`:

```ts
import { openDB, type IDBPDatabase } from 'idb';

interface CachedChunk {
  sessionId: string;
  chunkIndex: number;
  blob: Blob;
  createdAt: number;
}

const STORE_NAME = 'chunks';

export class ChunkCache {
  private dbPromise: Promise<IDBPDatabase>;

  constructor(dbName = 'mymemo-chunks') {
    this.dbPromise = openDB(dbName, 1, {
      upgrade(db) {
        const store = db.createObjectStore(STORE_NAME, {
          keyPath: ['sessionId', 'chunkIndex'],
        });
        store.createIndex('bySession', 'sessionId');
      },
    });
  }

  async store(sessionId: string, chunkIndex: number, blob: Blob) {
    const db = await this.dbPromise;
    await db.put(STORE_NAME, {
      sessionId,
      chunkIndex,
      blob,
      createdAt: Date.now(),
    });
  }

  async getPending(sessionId: string): Promise<CachedChunk[]> {
    const db = await this.dbPromise;
    const all = await db.getAllFromIndex(STORE_NAME, 'bySession', sessionId);
    return all.sort((a, b) => a.chunkIndex - b.chunkIndex);
  }

  async markUploaded(sessionId: string, chunkIndex: number) {
    const db = await this.dbPromise;
    await db.delete(STORE_NAME, [sessionId, chunkIndex]);
  }

  async clearSession(sessionId: string) {
    const db = await this.dbPromise;
    const tx = db.transaction(STORE_NAME, 'readwrite');
    const index = tx.store.index('bySession');
    let cursor = await index.openCursor(sessionId);
    while (cursor) {
      await cursor.delete();
      cursor = await cursor.continue();
    }
    await tx.done;
  }
}
```

**Step 5: Run tests to verify they pass**

```bash
cd web && npm test -- src/services/chunk-cache.test.ts
```

Expected: All tests pass.

**Step 6: Commit**

```bash
git add web/src/services/chunk-cache.ts web/src/services/chunk-cache.test.ts
git commit -m "feat(web): add IndexedDB chunk cache for offline resilience"
```

---

### Task 12: Recorder Store (TDD)

**Files:**
- Create: `web/src/stores/recorder.ts`
- Create: `web/src/stores/recorder.test.ts`

**Step 1: Write failing tests**

`web/src/stores/recorder.test.ts`:

```ts
import { describe, test, expect, vi, beforeEach } from 'vitest';
import { useRecorderStore, type LocalChunk } from './recorder';

vi.mock('../api/client', () => ({
  api: {
    sessions: { create: vi.fn() },
    chunks: { upload: vi.fn() },
    memos: { finalize: vi.fn() },
  },
}));

vi.mock('../services/audio', () => ({
  AudioCaptureService: vi.fn(() => ({
    getStream: vi.fn().mockResolvedValue({
      getTracks: () => [],
      getAudioTracks: () => [],
    }),
    stop: vi.fn(),
  })),
}));

vi.mock('../services/chunk-cache', () => ({
  ChunkCache: vi.fn(() => ({
    store: vi.fn(),
    markUploaded: vi.fn(),
    getPending: vi.fn().mockResolvedValue([]),
    clearSession: vi.fn(),
  })),
}));

// Mock MediaRecorder
const mockMediaRecorder = {
  start: vi.fn(),
  stop: vi.fn(),
  ondataavailable: null as ((e: { data: Blob }) => void) | null,
  onstop: null as (() => void) | null,
  state: 'inactive',
};
vi.stubGlobal(
  'MediaRecorder',
  vi.fn(() => mockMediaRecorder),
);

import { api } from '../api/client';

beforeEach(() => {
  useRecorderStore.setState({
    status: 'idle',
    sessionId: null,
    chunks: [],
    elapsedMs: 0,
    audioSource: 'microphone',
    outputMode: 'full',
  });
  vi.clearAllMocks();
});

describe('recorder store', () => {
  test('initial state is idle', () => {
    const state = useRecorderStore.getState();
    expect(state.status).toBe('idle');
    expect(state.sessionId).toBeNull();
    expect(state.chunks).toEqual([]);
  });

  test('setAudioSource updates audio source', () => {
    useRecorderStore.getState().setAudioSource('both');
    expect(useRecorderStore.getState().audioSource).toBe('both');
  });

  test('setOutputMode updates output mode', () => {
    useRecorderStore.getState().setOutputMode('summary');
    expect(useRecorderStore.getState().outputMode).toBe('summary');
  });

  test('startRecording creates session and changes status', async () => {
    vi.mocked(api.sessions.create).mockResolvedValue({
      id: 'new-sess',
      userId: 'u1',
      title: null,
      status: 'recording',
      outputMode: 'full',
      audioSource: 'microphone',
      startedAt: '2026-01-01T00:00:00',
      endedAt: null,
      createdAt: '2026-01-01T00:00:00',
      updatedAt: '2026-01-01T00:00:00',
    });

    await useRecorderStore.getState().startRecording();

    expect(api.sessions.create).toHaveBeenCalledWith({
      outputMode: 'full',
      audioSource: 'microphone',
    });
    expect(useRecorderStore.getState().status).toBe('recording');
    expect(useRecorderStore.getState().sessionId).toBe('new-sess');
  });

  test('addChunk appends chunk to list', () => {
    useRecorderStore.getState().addChunk({
      chunkIndex: 0,
      status: 'pending',
    });

    expect(useRecorderStore.getState().chunks).toHaveLength(1);
    expect(useRecorderStore.getState().chunks[0].chunkIndex).toBe(0);
  });

  test('updateChunkStatus changes specific chunk status', () => {
    useRecorderStore.getState().addChunk({ chunkIndex: 0, status: 'pending' });
    useRecorderStore.getState().addChunk({ chunkIndex: 1, status: 'pending' });

    useRecorderStore.getState().updateChunkStatus(0, 'uploaded');

    const chunks = useRecorderStore.getState().chunks;
    expect(chunks[0].status).toBe('uploaded');
    expect(chunks[1].status).toBe('pending');
  });

  test('reset clears all state', () => {
    useRecorderStore.setState({
      status: 'recording',
      sessionId: 'sess1',
      chunks: [{ chunkIndex: 0, status: 'uploaded' }],
      elapsedMs: 60000,
    });

    useRecorderStore.getState().reset();

    const state = useRecorderStore.getState();
    expect(state.status).toBe('idle');
    expect(state.sessionId).toBeNull();
    expect(state.chunks).toEqual([]);
    expect(state.elapsedMs).toBe(0);
  });
});
```

**Step 2: Run tests to verify they fail**

```bash
cd web && npm test -- src/stores/recorder.test.ts
```

Expected: FAIL — module `./recorder` not found.

**Step 3: Implement the recorder store**

`web/src/stores/recorder.ts`:

```ts
import { create } from 'zustand';
import { api } from '../api/client';
import { AudioCaptureService } from '../services/audio';
import { ChunkCache } from '../services/chunk-cache';
import type { AudioSource, OutputMode } from '../types';

export type LocalChunkStatus =
  | 'pending'
  | 'uploading'
  | 'uploaded'
  | 'failed';

export interface LocalChunk {
  chunkIndex: number;
  status: LocalChunkStatus;
}

type RecorderStatus = 'idle' | 'recording' | 'stopped' | 'finalizing';

interface RecorderState {
  status: RecorderStatus;
  sessionId: string | null;
  chunks: LocalChunk[];
  elapsedMs: number;
  audioSource: AudioSource;
  outputMode: OutputMode;

  setAudioSource: (source: AudioSource) => void;
  setOutputMode: (mode: OutputMode) => void;
  startRecording: () => Promise<void>;
  stopRecording: () => void;
  finalize: () => Promise<void>;
  addChunk: (chunk: LocalChunk) => void;
  updateChunkStatus: (index: number, status: LocalChunkStatus) => void;
  setElapsedMs: (ms: number) => void;
  reset: () => void;
}

const CHUNK_INTERVAL_MS = 5 * 60 * 1000; // 5 minutes

let audioService: AudioCaptureService | null = null;
let mediaRecorder: MediaRecorder | null = null;
let timerInterval: ReturnType<typeof setInterval> | null = null;
let startTime: number | null = null;
const chunkCache = new ChunkCache();

export const useRecorderStore = create<RecorderState>((set, get) => ({
  status: 'idle',
  sessionId: null,
  chunks: [],
  elapsedMs: 0,
  audioSource: 'microphone',
  outputMode: 'full',

  setAudioSource: (audioSource) => set({ audioSource }),
  setOutputMode: (outputMode) => set({ outputMode }),

  startRecording: async () => {
    const { audioSource, outputMode } = get();

    const session = await api.sessions.create({ outputMode, audioSource });
    set({ sessionId: session.id, status: 'recording', chunks: [] });

    audioService = new AudioCaptureService();
    const stream = await audioService.getStream(audioSource);

    mediaRecorder = new MediaRecorder(stream, {
      mimeType: 'audio/webm;codecs=opus',
    });

    let chunkIndex = 0;

    mediaRecorder.ondataavailable = async (e) => {
      if (e.data.size === 0) return;

      const idx = chunkIndex++;
      get().addChunk({ chunkIndex: idx, status: 'pending' });

      await chunkCache.store(session.id, idx, e.data);
      get().updateChunkStatus(idx, 'uploading');

      try {
        await api.chunks.upload(session.id, e.data, idx);
        await chunkCache.markUploaded(session.id, idx);
        get().updateChunkStatus(idx, 'uploaded');
      } catch {
        get().updateChunkStatus(idx, 'failed');
      }
    };

    mediaRecorder.start(CHUNK_INTERVAL_MS);

    startTime = Date.now();
    timerInterval = setInterval(() => {
      if (startTime) {
        set({ elapsedMs: Date.now() - startTime });
      }
    }, 1000);
  },

  stopRecording: () => {
    mediaRecorder?.stop();
    audioService?.stop();
    if (timerInterval) clearInterval(timerInterval);
    timerInterval = null;
    set({ status: 'stopped' });
  },

  finalize: async () => {
    const { sessionId } = get();
    if (!sessionId) return;

    set({ status: 'finalizing' });
    await api.memos.finalize(sessionId);
  },

  addChunk: (chunk) =>
    set((state) => ({ chunks: [...state.chunks, chunk] })),

  updateChunkStatus: (index, status) =>
    set((state) => ({
      chunks: state.chunks.map((c) =>
        c.chunkIndex === index ? { ...c, status } : c,
      ),
    })),

  setElapsedMs: (ms) => set({ elapsedMs: ms }),

  reset: () => {
    mediaRecorder?.stop();
    audioService?.stop();
    if (timerInterval) clearInterval(timerInterval);
    timerInterval = null;
    startTime = null;
    set({
      status: 'idle',
      sessionId: null,
      chunks: [],
      elapsedMs: 0,
    });
  },
}));
```

**Step 4: Run tests to verify they pass**

```bash
cd web && npm test -- src/stores/recorder.test.ts
```

Expected: All tests pass.

**Step 5: Commit**

```bash
git add web/src/stores/recorder.ts web/src/stores/recorder.test.ts
git commit -m "feat(web): add recorder store with recording state machine"
```

---

### Task 13: Recorder Page (TDD)

**Files:**
- Create: `web/src/components/AudioSourcePicker.tsx`
- Create: `web/src/components/RecordingTimer.tsx`
- Create: `web/src/components/ChunkStatusList.tsx`
- Modify: `web/src/pages/RecorderPage.tsx`
- Create: `web/src/pages/RecorderPage.test.tsx`

**Step 1: Write failing tests**

`web/src/pages/RecorderPage.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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
  AudioCaptureService: vi.fn(() => ({
    getStream: vi.fn().mockResolvedValue({ getTracks: () => [] }),
    stop: vi.fn(),
  })),
}));

vi.mock('../services/chunk-cache', () => ({
  ChunkCache: vi.fn(() => ({
    store: vi.fn(),
    markUploaded: vi.fn(),
    getPending: vi.fn().mockResolvedValue([]),
  })),
}));

vi.stubGlobal('MediaRecorder', vi.fn(() => ({
  start: vi.fn(),
  stop: vi.fn(),
  ondataavailable: null,
})));

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
```

**Step 2: Run tests to verify they fail**

```bash
cd web && npm test -- src/pages/RecorderPage.test.tsx
```

Expected: FAIL — RecorderPage doesn't render expected elements.

**Step 3: Create helper components**

`web/src/components/AudioSourcePicker.tsx`:

```tsx
import type { AudioSource, OutputMode } from '../types';

interface Props {
  audioSource: AudioSource;
  outputMode: OutputMode;
  onAudioSourceChange: (source: AudioSource) => void;
  onOutputModeChange: (mode: OutputMode) => void;
}

export default function AudioSourcePicker({
  audioSource,
  outputMode,
  onAudioSourceChange,
  onOutputModeChange,
}: Props) {
  return (
    <div className="space-y-4">
      <div>
        <label
          htmlFor="audio-source"
          className="block text-sm font-medium text-gray-700"
        >
          Audio Source
        </label>
        <select
          id="audio-source"
          value={audioSource}
          onChange={(e) => onAudioSourceChange(e.target.value as AudioSource)}
          className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2"
        >
          <option value="microphone">Microphone</option>
          <option value="system">System Audio</option>
          <option value="both">Both (Mic + System)</option>
        </select>
      </div>
      <div>
        <label
          htmlFor="output-mode"
          className="block text-sm font-medium text-gray-700"
        >
          Output Mode
        </label>
        <select
          id="output-mode"
          value={outputMode}
          onChange={(e) => onOutputModeChange(e.target.value as OutputMode)}
          className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2"
        >
          <option value="full">Full Transcript</option>
          <option value="summary">Summary</option>
        </select>
      </div>
    </div>
  );
}
```

`web/src/components/RecordingTimer.tsx`:

```tsx
interface Props {
  elapsedMs: number;
}

export default function RecordingTimer({ elapsedMs }: Props) {
  const totalSec = Math.floor(elapsedMs / 1000);
  const hours = String(Math.floor(totalSec / 3600)).padStart(2, '0');
  const minutes = String(Math.floor((totalSec % 3600) / 60)).padStart(2, '0');
  const seconds = String(totalSec % 60).padStart(2, '0');

  return (
    <span className="font-mono text-2xl text-gray-900">
      {hours}:{minutes}:{seconds}
    </span>
  );
}
```

`web/src/components/ChunkStatusList.tsx`:

```tsx
import type { LocalChunk } from '../stores/recorder';

const statusIcons: Record<string, string> = {
  pending: '...',
  uploading: '>>',
  uploaded: 'OK',
  failed: '!!',
};

const statusStyles: Record<string, string> = {
  pending: 'text-gray-400',
  uploading: 'text-blue-500',
  uploaded: 'text-green-600',
  failed: 'text-red-500',
};

interface Props {
  chunks: LocalChunk[];
}

export default function ChunkStatusList({ chunks }: Props) {
  if (chunks.length === 0) return null;

  return (
    <div className="space-y-1">
      <h3 className="text-sm font-medium text-gray-700">Chunks</h3>
      {chunks.map((chunk) => (
        <div key={chunk.chunkIndex} className="flex items-center gap-2 text-sm">
          <span className={statusStyles[chunk.status]}>
            [{statusIcons[chunk.status]}]
          </span>
          <span>Chunk {chunk.chunkIndex + 1}</span>
          <span className="text-gray-400">{chunk.status}</span>
        </div>
      ))}
    </div>
  );
}
```

**Step 4: Implement RecorderPage**

`web/src/pages/RecorderPage.tsx`:

```tsx
import { useNavigate } from 'react-router-dom';
import { useRecorderStore } from '../stores/recorder';
import AudioSourcePicker from '../components/AudioSourcePicker';
import RecordingTimer from '../components/RecordingTimer';
import ChunkStatusList from '../components/ChunkStatusList';

export default function RecorderPage() {
  const navigate = useNavigate();
  const {
    status,
    sessionId,
    chunks,
    elapsedMs,
    audioSource,
    outputMode,
    setAudioSource,
    setOutputMode,
    startRecording,
    stopRecording,
    finalize,
  } = useRecorderStore();

  const handleStart = async () => {
    await startRecording();
  };

  const handleStop = () => {
    stopRecording();
  };

  const handleFinalize = async () => {
    await finalize();
    navigate(`/sessions/${sessionId}`);
  };

  return (
    <div className="mx-auto max-w-lg space-y-6">
      <h1 className="text-lg font-semibold text-gray-900">New Recording</h1>

      {status === 'idle' && (
        <>
          <AudioSourcePicker
            audioSource={audioSource}
            outputMode={outputMode}
            onAudioSourceChange={setAudioSource}
            onOutputModeChange={setOutputMode}
          />
          <button
            onClick={handleStart}
            className="w-full rounded-lg bg-red-600 px-4 py-3 font-medium text-white hover:bg-red-700"
          >
            Start Recording
          </button>
        </>
      )}

      {(status === 'recording' || status === 'stopped') && (
        <div className="space-y-4">
          <div className="flex items-center gap-4">
            {status === 'recording' && (
              <span className="h-3 w-3 animate-pulse rounded-full bg-red-500" />
            )}
            <RecordingTimer elapsedMs={elapsedMs} />
          </div>

          {status === 'recording' && (
            <button
              onClick={handleStop}
              className="w-full rounded-lg bg-gray-800 px-4 py-3 font-medium text-white hover:bg-gray-900"
            >
              Stop Recording
            </button>
          )}

          {status === 'stopped' && (
            <button
              onClick={handleFinalize}
              className="w-full rounded-lg bg-blue-600 px-4 py-3 font-medium text-white hover:bg-blue-700"
            >
              Finalize & Generate Memo
            </button>
          )}

          <ChunkStatusList chunks={chunks} />
        </div>
      )}

      {status === 'finalizing' && (
        <div className="py-8 text-center text-gray-500">
          Finalizing session...
        </div>
      )}
    </div>
  );
}
```

**Step 5: Run tests to verify they pass**

```bash
cd web && npm test -- src/pages/RecorderPage.test.tsx
```

Expected: All tests pass.

**Step 6: Commit**

```bash
git add web/src/pages/RecorderPage.tsx web/src/pages/RecorderPage.test.tsx web/src/components/AudioSourcePicker.tsx web/src/components/RecordingTimer.tsx web/src/components/ChunkStatusList.tsx
git commit -m "feat(web): add recorder page with audio source picker and chunk status"
```

---

### Task 14: Session Detail Page + Memo Polling (TDD)

**Files:**
- Create: `web/src/components/MemoViewer.tsx`
- Modify: `web/src/pages/SessionDetailPage.tsx`
- Create: `web/src/pages/SessionDetailPage.test.tsx`

**Step 1: Write failing tests**

`web/src/pages/SessionDetailPage.test.tsx`:

```tsx
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
  setTokenProvider: vi.fn(),
}));

import { api } from '../api/client';
import SessionDetailPage from './SessionDetailPage';

const mockDetail = {
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

const mockMemo = {
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
```

**Step 2: Run tests to verify they fail**

```bash
cd web && npm test -- src/pages/SessionDetailPage.test.tsx
```

Expected: FAIL — SessionDetailPage doesn't render expected elements.

**Step 3: Create MemoViewer component**

`web/src/components/MemoViewer.tsx`:

```tsx
import type { Memo } from '../types';

interface Props {
  memo: Memo | null;
  isProcessing: boolean;
}

export default function MemoViewer({ memo, isProcessing }: Props) {
  if (isProcessing && !memo) {
    return (
      <div className="rounded-lg border border-blue-200 bg-blue-50 p-4">
        <p className="text-blue-700">Generating memo... This may take a moment.</p>
      </div>
    );
  }

  if (!memo) return null;

  return (
    <div className="space-y-2">
      <div className="flex items-center gap-2">
        <h2 className="text-lg font-semibold text-gray-900">Memo</h2>
        <span className="text-xs text-gray-400">
          {memo.outputMode === 'full' ? 'Full Transcript' : 'Summary'}
        </span>
      </div>
      <div className="prose max-w-none rounded-lg border bg-white p-4">
        <div className="whitespace-pre-wrap">{memo.content}</div>
      </div>
      <p className="text-xs text-gray-400">
        Model: {memo.modelUsed} | Tokens: {memo.promptTokens ?? 0} + {memo.completionTokens ?? 0}
      </p>
    </div>
  );
}
```

**Step 4: Implement SessionDetailPage with memo polling**

`web/src/pages/SessionDetailPage.tsx`:

```tsx
import { useEffect, useState, useRef } from 'react';
import { useParams, Link } from 'react-router-dom';
import { api, ApiError } from '../api/client';
import type { SessionDetail, Memo, ChunkStatus } from '../types';
import MemoViewer from '../components/MemoViewer';

const chunkStatusStyles: Record<ChunkStatus, string> = {
  uploaded: 'text-gray-400',
  queued: 'text-gray-400',
  transcribing: 'text-blue-500',
  transcribed: 'text-green-600',
  failed: 'text-red-500',
};

export default function SessionDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [detail, setDetail] = useState<SessionDetail | null>(null);
  const [memo, setMemo] = useState<Memo | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const pollRef = useRef<ReturnType<typeof setInterval>>();

  useEffect(() => {
    if (!id) return;

    async function load() {
      try {
        const data = await api.sessions.get(id!);
        setDetail(data);

        try {
          const m = await api.memos.get(id!);
          setMemo(m);
        } catch {
          // Memo not ready yet — that's ok
        }
      } catch (e) {
        setError((e as Error).message);
      } finally {
        setLoading(false);
      }
    }

    load();
  }, [id]);

  // Poll for memo when session is processing
  useEffect(() => {
    if (!id || !detail || detail.session.status !== 'processing') return;
    if (memo) return;

    pollRef.current = setInterval(async () => {
      try {
        const m = await api.memos.get(id);
        setMemo(m);
        setDetail((prev) =>
          prev
            ? {
                ...prev,
                session: { ...prev.session, status: 'completed' },
              }
            : prev,
        );
        clearInterval(pollRef.current);
      } catch {
        // Still processing
      }
    }, 5000);

    return () => clearInterval(pollRef.current);
  }, [id, detail, memo]);

  if (loading) {
    return <div className="py-8 text-center text-gray-500">Loading...</div>;
  }

  if (error || !detail) {
    return (
      <div className="py-8 text-center text-red-500">
        {error ?? 'Session not found'}
      </div>
    );
  }

  const { session, chunks } = detail;

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link to="/" className="text-sm text-gray-500 hover:text-gray-700">
          Back
        </Link>
        <h1 className="text-lg font-semibold text-gray-900">
          Session
        </h1>
        <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium">
          {session.status}
        </span>
      </div>

      <div className="text-sm text-gray-500">
        {session.outputMode === 'full' ? 'Full Transcript' : 'Summary'} | {session.audioSource}
      </div>

      {chunks.length > 0 && (
        <div className="space-y-1">
          <h2 className="text-sm font-medium text-gray-700">Chunks</h2>
          {chunks.map((chunk) => (
            <div
              key={chunk.id}
              className="flex items-center gap-2 text-sm"
            >
              <span className={chunkStatusStyles[chunk.status]}>
                Chunk {chunk.chunkIndex + 1}
              </span>
              <span className="text-gray-400">{chunk.status}</span>
            </div>
          ))}
        </div>
      )}

      <MemoViewer
        memo={memo}
        isProcessing={session.status === 'processing'}
      />
    </div>
  );
}
```

**Step 5: Run tests to verify they pass**

```bash
cd web && npm test -- src/pages/SessionDetailPage.test.tsx
```

Expected: All tests pass.

**Step 6: Commit**

```bash
git add web/src/pages/SessionDetailPage.tsx web/src/pages/SessionDetailPage.test.tsx web/src/components/MemoViewer.tsx
git commit -m "feat(web): add session detail page with memo polling"
```

---

### Task 15: Error Handling

**Files:**
- Create: `web/src/components/ErrorBoundary.tsx`
- Modify: `web/src/App.tsx`

**Step 1: Create error boundary**

`web/src/components/ErrorBoundary.tsx`:

```tsx
import { Component, type ReactNode } from 'react';

interface Props {
  children: ReactNode;
}

interface State {
  error: Error | null;
}

export default class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error) {
    return { error };
  }

  render() {
    if (this.state.error) {
      return (
        <div className="flex min-h-screen items-center justify-center">
          <div className="text-center">
            <h1 className="text-lg font-semibold text-gray-900">
              Something went wrong
            </h1>
            <p className="mt-2 text-sm text-gray-500">
              {this.state.error.message}
            </p>
            <button
              onClick={() => {
                this.setState({ error: null });
                window.location.href = '/';
              }}
              className="mt-4 rounded-lg bg-blue-600 px-4 py-2 text-sm text-white hover:bg-blue-700"
            >
              Go Home
            </button>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
```

**Step 2: Wrap App with ErrorBoundary**

In `web/src/App.tsx`, wrap the `<BrowserRouter>` with `<ErrorBoundary>`:

```tsx
import ErrorBoundary from './components/ErrorBoundary';

export default function App() {
  return (
    <ErrorBoundary>
      <BrowserRouter>
        {/* ...existing routes... */}
      </BrowserRouter>
    </ErrorBoundary>
  );
}
```

**Step 3: Run all tests**

```bash
cd web && npm test
```

Expected: All tests pass.

**Step 4: Commit**

```bash
git add web/src/components/ErrorBoundary.tsx web/src/App.tsx
git commit -m "feat(web): add error boundary for graceful error handling"
```

---

### Task 16: Netlify Deployment Config

**Files:**
- Create: `web/netlify.toml`
- Modify: `web/.env.example` (already created, verify contents)

**Step 1: Create Netlify config**

`web/netlify.toml`:

```toml
[build]
  command = "npm run build"
  publish = "dist"

[[redirects]]
  from = "/*"
  to = "/index.html"
  status = 200
```

**Step 2: Verify build works**

```bash
cd web && npm run build
```

Expected: Build succeeds, outputs to `web/dist/`.

**Step 3: Add dist to .gitignore**

Add to `web/.gitignore` (create if not present, or modify the Vite-generated one):

Ensure `dist` is in `.gitignore`.

**Step 4: Commit**

```bash
git add web/netlify.toml
git commit -m "feat(web): add Netlify deployment config"
```

---

### Task 17: Update README

**Files:**
- Modify: `web/README.md`

**Step 1: Write README**

`web/README.md`:

```markdown
# MyMemo — Web

React SPA frontend for MyMemo. Records audio, uploads chunks to the API, and displays transcription memos.

## Tech Stack

- React + TypeScript + Vite
- Tailwind CSS v4
- Zustand (state management)
- React Router (routing)
- Clerk (authentication)
- IndexedDB (offline chunk caching)

## Getting Started

```bash
npm install
cp .env.example .env
# Fill in VITE_CLERK_PUBLISHABLE_KEY and VITE_API_URL in .env
npm run dev
```

## Scripts

| Command | Description |
|---------|-------------|
| `npm run dev` | Start dev server |
| `npm run build` | Production build |
| `npm run preview` | Preview production build |
| `npm test` | Run tests once |
| `npm run test:watch` | Run tests in watch mode |

## Environment Variables

| Variable | Description |
|----------|-------------|
| `VITE_CLERK_PUBLISHABLE_KEY` | Clerk publishable key |
| `VITE_API_URL` | Backend API base URL |
```

**Step 2: Commit**

```bash
git add web/README.md
git commit -m "docs(web): update README with setup instructions"
```

---

## Done

All tasks complete. The web frontend connects to the backend API at the following endpoints:

| Frontend Action | API Endpoint |
|----------------|--------------|
| Create session | `POST /api/sessions` |
| List sessions | `GET /api/sessions` |
| Get session + chunks | `GET /api/sessions/{id}` |
| Delete session | `DELETE /api/sessions/{id}` |
| Upload chunk | `POST /api/sessions/{id}/chunks` |
| Finalize session | `POST /api/sessions/{id}/finalize` |
| Get memo | `GET /api/sessions/{id}/memo` |
