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
        <div className="flex min-h-screen items-center justify-center bg-bg-primary bg-[radial-gradient(ellipse_at_top,var(--color-accent-light)_0%,transparent_50%)]">
          <div className="text-center animate-[fadeInUp_0.3s_ease-out]">
            <svg className="mx-auto h-10 w-10 mb-4" viewBox="0 0 28 28" fill="none">
              <rect x="4" y="10" width="4" height="8" rx="2" fill="currentColor" className="text-accent" />
              <rect x="12" y="4" width="4" height="20" rx="2" fill="currentColor" className="text-accent" />
              <rect x="20" y="8" width="4" height="12" rx="2" fill="currentColor" className="text-accent" />
            </svg>
            <h1 className="font-heading text-lg font-semibold text-text-primary">
              Something went wrong
            </h1>
            <p className="mt-2 text-sm text-text-secondary max-w-sm mx-auto">
              {this.state.error.message}
            </p>
            <button
              onClick={() => {
                this.setState({ error: null });
                window.location.href = '/';
              }}
              className="mt-4 rounded-lg bg-accent px-4 py-2.5 text-sm font-medium text-white hover:bg-accent-hover transition-colors"
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
