import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { ClerkProvider } from '@clerk/clerk-react';
import { MsalProvider } from '@azure/msal-react';
import App from './App';
import { msalInstance } from './config/msalConfig';
import './index.css';

const clerkPubKey = import.meta.env.VITE_CLERK_PUBLISHABLE_KEY;

if (!clerkPubKey) {
  throw new Error('Missing VITE_CLERK_PUBLISHABLE_KEY env variable');
}

// If this page load is an MSAL popup auth redirect, initialize MSAL only —
// it will process the auth code and close the popup without rendering the app.
const isAuthPopupRedirect =
  window.opener !== null &&
  window.opener !== window &&
  (window.location.hash.includes('code=') || window.location.search.includes('code='));

if (isAuthPopupRedirect) {
  msalInstance.initialize();
} else {
  msalInstance.initialize().then(() => {
    createRoot(document.getElementById('root')!).render(
      <StrictMode>
        <MsalProvider instance={msalInstance}>
          <ClerkProvider publishableKey={clerkPubKey}>
            <App />
          </ClerkProvider>
        </MsalProvider>
      </StrictMode>,
    );
  });
}
