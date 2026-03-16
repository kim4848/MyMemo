import { SignIn } from '@clerk/clerk-react';

export default function LoginPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-bg-primary">
      <div className="text-center">
        <h1 className="mb-6 text-2xl font-bold text-text-primary tracking-wide">
          My<span className="text-accent">Memo</span>
        </h1>
        <SignIn
          routing="hash"
          appearance={{
            variables: {
              colorPrimary: '#2563EB',
              borderRadius: '0.5rem',
            },
          }}
        />
      </div>
    </div>
  );
}
