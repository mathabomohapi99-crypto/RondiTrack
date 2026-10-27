import { useEffect, useState } from 'react';
import { logout, onSignedOut, refreshAccessToken } from './auth/session';
import { Dashboard } from './components/Dashboard';
import { LoginForm } from './components/LoginForm';
import { queryClient } from './lib/queryClient';
import { clearSelectedStokvel } from './state/selectedStokvel';

type Status = 'checking' | 'signedIn' | 'signedOut';

export default function App() {
  const [status, setStatus] = useState<Status>('checking');

  useEffect(() => {
    // Sign-out (button OR failed refresh): show the form, clear the cache AND the selected stokvel
    const stopListening = onSignedOut(() => {
      setStatus('signedOut');
      clearSelectedStokvel();
      queryClient.clear();
    });
    // After a page reload, try the cookie once to see if the member is still signed in
    void refreshAccessToken().then((token) => setStatus(token ? 'signedIn' : 'signedOut'));
    return stopListening;
  }, []);

  if (status === 'checking') return <p>Loading…</p>;
  if (status === 'signedOut') return <LoginForm onSignedIn={() => setStatus('signedIn')} />;

  return (
    <main style={{ maxWidth: 640, margin: '0 auto', padding: 16 }}>
      <header style={{ display: 'flex', justifyContent: 'space-between' }}>
        <h1>RondiTrack</h1>
        <button onClick={() => void logout()}>Sign out</button>
      </header>
      <Dashboard />
    </main>
  );
}