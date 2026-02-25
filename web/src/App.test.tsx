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
