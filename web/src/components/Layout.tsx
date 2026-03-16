import { Link, NavLink, Outlet, useNavigate, useLocation } from 'react-router-dom';
import { useAuth, UserButton } from '@clerk/clerk-react';
import { useEffect, useState } from 'react';
import { setTokenProvider } from '../api/client';
import { setNotificationNavigate, stopNotificationPoller } from '../services/notifications';
import { useTheme } from '../hooks/useTheme';
import ToastContainer from './ToastContainer';

export default function Layout() {
  const { isSignedIn, isLoaded, getToken } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const { theme, toggleTheme } = useTheme();

  useEffect(() => {
    if (isLoaded && !isSignedIn) {
      navigate('/login');
    }
  }, [isLoaded, isSignedIn, navigate]);

  useEffect(() => {
    setTokenProvider(() => getToken());
  }, [getToken]);

  // Close sidebar on route change
  useEffect(() => {
    setSidebarOpen(false);
  }, [location.pathname]);

  // Provide navigate to the global notification service
  useEffect(() => {
    setNotificationNavigate(navigate);
    return () => stopNotificationPoller();
  }, [navigate]);

  if (!isLoaded) {
    return (
      <div className="flex min-h-dvh items-center justify-center bg-bg-primary">
        <div className="text-text-muted">Loading...</div>
      </div>
    );
  }

  if (!isSignedIn) return null;

  const navLink = ({ isActive }: { isActive: boolean }) =>
    `flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors ${
      isActive
        ? 'bg-accent-light text-accent'
        : 'text-text-secondary hover:bg-bg-hover hover:text-text-primary'
    }`;

  const sidebarContent = (
    <>
      <div className="px-5 py-6">
        <Link to="/" className="text-xl font-bold text-text-primary tracking-wide">
          My<span className="text-accent">Memo</span>
        </Link>
      </div>

      <nav className="flex-1 space-y-1 px-3">
        <NavLink to="/" end className={navLink}>
          <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" d="M3.75 12h16.5m-16.5 3.75h16.5M3.75 19.5h16.5M5.625 4.5h12.75a1.875 1.875 0 0 1 0 3.75H5.625a1.875 1.875 0 0 1 0-3.75Z" />
          </svg>
          Sessions
        </NavLink>
        <NavLink to="/record" className={navLink}>
          <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 18.75a6 6 0 0 0 6-6v-1.5m-6 7.5a6 6 0 0 1-6-6v-1.5m6 7.5v3.75m-3.75 0h7.5M12 15.75a3 3 0 0 1-3-3V4.5a3 3 0 1 1 6 0v8.25a3 3 0 0 1-3 3Z" />
          </svg>
          New Recording
        </NavLink>
      </nav>

      <div className="border-t border-border px-4 py-4">
        <div className="flex items-center gap-3">
          <UserButton
            appearance={{
              elements: {
                avatarBox: 'h-8 w-8',
              },
            }}
          />
          <span className="text-sm text-text-secondary truncate">Account</span>
          <button
            onClick={toggleTheme}
            className="ml-auto rounded-lg p-2 text-text-muted hover:bg-bg-hover hover:text-text-primary transition-colors"
            aria-label="Toggle theme"
          >
            {theme === 'light' ? (
              <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" d="M21.752 15.002A9.72 9.72 0 0 1 18 15.75c-5.385 0-9.75-4.365-9.75-9.75 0-1.33.266-2.597.748-3.752A9.753 9.753 0 0 0 3 11.25C3 16.635 7.365 21 12.75 21a9.753 9.753 0 0 0 9.002-5.998Z" />
              </svg>
            ) : (
              <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 3v2.25m6.364.386-1.591 1.591M21 12h-2.25m-.386 6.364-1.591-1.591M12 18.75V21m-4.773-4.227-1.591 1.591M5.25 12H3m4.227-4.773L5.636 5.636M15.75 12a3.75 3.75 0 1 1-7.5 0 3.75 3.75 0 0 1 7.5 0Z" />
              </svg>
            )}
          </button>
        </div>
      </div>
    </>
  );

  return (
    <div className="flex min-h-dvh bg-bg-primary">
      {/* Mobile top bar */}
      <div className="fixed inset-x-0 top-0 z-20 flex h-14 items-center justify-between border-b border-border bg-bg-card px-4 md:hidden">
        <Link to="/" className="text-lg font-bold text-text-primary tracking-wide">
          My<span className="text-accent">Memo</span>
        </Link>
        <button
          onClick={() => setSidebarOpen(!sidebarOpen)}
          className="rounded-lg p-2 text-text-muted hover:bg-bg-hover hover:text-text-primary"
          aria-label="Toggle menu"
        >
          {sidebarOpen ? (
            <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18 18 6M6 6l12 12" />
            </svg>
          ) : (
            <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5m-16.5 5.25h16.5" />
            </svg>
          )}
        </button>
      </div>

      {/* Mobile sidebar overlay */}
      {sidebarOpen && (
        <div
          className="fixed inset-0 z-30 bg-black/50 md:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {/* Mobile sidebar drawer */}
      <aside
        className={`fixed inset-y-0 left-0 z-40 flex w-64 flex-col border-r border-border bg-bg-sidebar shadow-lg transition-transform duration-200 md:hidden ${
          sidebarOpen ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        {sidebarContent}
      </aside>

      {/* Desktop sidebar */}
      <aside className="fixed inset-y-0 left-0 z-10 hidden w-56 flex-col border-r border-border bg-bg-sidebar md:flex">
        {sidebarContent}
      </aside>

      {/* Main content */}
      <main className="flex-1 pt-14 px-4 pb-6 md:ml-56 md:py-10 md:px-8">
        <div className="mx-auto max-w-4xl">
          <Outlet />
        </div>
      </main>

      <ToastContainer />
    </div>
  );
}
