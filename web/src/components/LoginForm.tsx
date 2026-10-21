import { useState } from 'react';
import { login } from '../auth/session';
import { describeError } from '../lib/errorMessages';
import { Reference } from './ErrorView';

export function LoginForm({ onSignedIn }: { onSignedIn: () => void }) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await login(email, password);
      onSignedIn();
    } catch (err) {
      setError(err); // shown through describeError below
    } finally {
      setBusy(false);
    }
  }

  const d = error ? describeError(error, 'login') : null;

  return (
    <form onSubmit={submit} style={{ display: 'grid', gap: 8, maxWidth: 320 }}>
      <h1>Sign in to RondiTrack</h1>
      <label htmlFor="email">Email</label>
      <input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
      {d?.fieldErrors.email?.map((m) => <small key={m}>{m}</small>)}
      <label htmlFor="password">Password</label>
      <input id="password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
      {d?.fieldErrors.password?.map((m) => <small key={m}>{m}</small>)}
      {d && d.kind !== 'validation' && (
        <div role="alert">
          <p>{d.message}</p>
          <Reference id={d.correlationId} />
        </div>
      )}
      <button type="submit" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</button>
    </form>
  );
}