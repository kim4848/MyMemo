import { Link, NavLink, Outlet, useNavigate, useLocation } from 'react-router-dom';
import { useAuth, UserButton } from '@clerk/clerk-react';
import { useEffect, useState } from 'react';
import { setTokenProvider } from '../api/client';

export default function Layout() {
  const { isSignedIn, isLoaded, getToken } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [sidebarOpen, setSidebarOpen] = useState(false);

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

  if (!isLoaded) {
    return (
      <div className="flex min-h-dvh items-center justify-center bg-navy-950">
        <div className="text-gray-400">Loading...</div>
      </div>
    );
  }

  if (!isSignedIn) return null;

  const navLink = ({ isActive }: { isActive: boolean }) =>
    `flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors ${
      isActive
        ? 'bg-navy-700 text-accent'
        : 'text-gray-400 hover:bg-navy-800 hover:text-gray-200'
    }`;

  const sidebarContent = (
    <>
      <div className="px-5 py-6">
        <Link to="/" className="text-xl font-bold text-white tracking-wide">
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

      <div className="border-t border-navy-700 px-4 py-4">
        <div className="flex items-center gap-3">
          <UserButton
            appearance={{
              elements: {
                avatarBox: 'h-8 w-8',
              },
            }}
          />
          <span className="text-sm text-gray-400 truncate">Account</span>
        </div>
      </div>
    </>
  );

  return (
    <div className="flex min-h-dvh bg-navy-950">
      {/* Mobile top bar */}
      <div className="fixed inset-x-0 top-0 z-20 flex h-14 items-center justify-between border-b border-navy-700 bg-navy-900 px-4 md:hidden">
        <Link to="/" className="text-lg font-bold text-white tracking-wide">
          My<span className="text-accent">Memo</span>
        </Link>
        <button
          onClick={() => setSidebarOpen(!sidebarOpen)}
          className="rounded-lg p-2 text-gray-400 hover:bg-navy-800 hover:text-white"
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
          className="fixed inset-0 z-30 bg-black/60 md:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {/* Mobile sidebar drawer */}
      <aside
        className={`fixed inset-y-0 left-0 z-40 flex w-64 flex-col border-r border-navy-700 bg-navy-900 transition-transform duration-200 md:hidden ${
          sidebarOpen ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        {sidebarContent}
      </aside>

      {/* Desktop sidebar */}
      <aside className="fixed inset-y-0 left-0 z-10 hidden w-56 flex-col border-r border-navy-700 bg-navy-900 md:flex">
        {sidebarContent}
      </aside>

      {/* Main content */}
      <main className="flex-1 pt-14 px-4 pb-6 md:ml-56 md:p-8 md:pt-8">
        <div className="mx-auto max-w-4xl">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
