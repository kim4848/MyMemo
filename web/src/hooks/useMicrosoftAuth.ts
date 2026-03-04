import { useMsal } from '@azure/msal-react';
import { InteractionRequiredAuthError } from '@azure/msal-browser';
import { msalScopes } from '../config/msalConfig';

export function useMicrosoftAuth() {
  const { instance, accounts } = useMsal();

  const isAuthenticated = accounts.length > 0;

  async function login() {
    await instance.loginPopup({ scopes: msalScopes, prompt: 'select_account' });
  }

  async function logout() {
    await instance.logoutPopup();
  }

  async function getAccessToken(): Promise<string> {
    const account = accounts[0];
    if (!account) {
      throw new Error('No Microsoft account signed in');
    }

    try {
      const result = await instance.acquireTokenSilent({
        scopes: msalScopes,
        account,
      });
      return result.accessToken;
    } catch (err) {
      if (err instanceof InteractionRequiredAuthError) {
        const result = await instance.acquireTokenPopup({
          scopes: msalScopes,
          account,
        });
        return result.accessToken;
      }
      throw err;
    }
  }

  return { isAuthenticated, login, logout, getAccessToken };
}
