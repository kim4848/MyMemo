import { SignIn } from '@clerk/clerk-react';

export default function LoginPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-bg-primary bg-[radial-gradient(ellipse_at_top,var(--color-accent-light)_0%,transparent_50%)]">
      <div className="text-center animate-[fadeInUp_0.4s_ease-out]">
        <div className="flex items-center justify-center gap-2 mb-2">
          <svg className="h-8 w-8" viewBox="0 0 28 28" fill="none">
            <rect x="4" y="10" width="4" height="8" rx="2" fill="currentColor" className="text-accent" />
            <rect x="12" y="4" width="4" height="20" rx="2" fill="currentColor" className="text-accent" />
            <rect x="20" y="8" width="4" height="12" rx="2" fill="currentColor" className="text-accent" />
          </svg>
          <h1 className="text-2xl font-bold text-text-primary tracking-wide">
            My<span className="text-accent">Memo</span>
          </h1>
        </div>
        <p className="text-text-secondary mb-8">AI-powered meeting transcription and notes</p>
        <SignIn
          routing="hash"
          appearance={{
            variables: {
              colorPrimary: '#2563EB',
              borderRadius: '0.5rem',
            },
          }}
        />
        <div className="mt-8 flex flex-col items-center gap-3 text-sm text-text-secondary">
          <div className="flex items-center gap-2">
            <svg className="h-4 w-4 text-accent" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 18.75a6 6 0 0 0 6-6v-1.5m-6 7.5a6 6 0 0 1-6-6v-1.5m6 7.5v3.75m-3.75 0h7.5M12 15.75a3 3 0 0 1-3-3V4.5a3 3 0 1 1 6 0v8.25a3 3 0 0 1-3 3Z" />
            </svg>
            Record meetings
          </div>
          <div className="flex items-center gap-2">
            <svg className="h-4 w-4 text-accent" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904 9 18.75l-.813-2.846a4.5 4.5 0 0 0-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 0 0 3.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 0 0 3.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 0 0-3.09 3.09ZM18.259 8.715 18 9.75l-.259-1.035a3.375 3.375 0 0 0-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 0 0 2.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 0 0 2.455 2.456L21.75 6l-1.036.259a3.375 3.375 0 0 0-2.455 2.456Z" />
            </svg>
            AI summaries
          </div>
          <div className="flex items-center gap-2">
            <svg className="h-4 w-4 text-accent" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 21a9.004 9.004 0 0 0 8.716-6.747M12 21a9.004 9.004 0 0 1-8.716-6.747M12 21c2.485 0 4.5-4.03 4.5-9S14.485 3 12 3m0 18c-2.485 0-4.5-4.03-4.5-9S9.515 3 12 3m0 0a8.997 8.997 0 0 1 7.843 4.582M12 3a8.997 8.997 0 0 0-7.843 4.582m15.686 0A11.953 11.953 0 0 1 12 10.5c-2.998 0-5.74-1.1-7.843-2.918m15.686 0A8.959 8.959 0 0 1 21 12c0 .778-.099 1.533-.284 2.253m0 0A17.919 17.919 0 0 1 12 16.5c-3.162 0-6.133-.815-8.716-2.247m0 0A9.015 9.015 0 0 1 3 12c0-1.605.42-3.113 1.157-4.418" />
            </svg>
            Danish &amp; English
          </div>
        </div>
      </div>
    </div>
  );
}
