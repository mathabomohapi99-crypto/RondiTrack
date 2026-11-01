'use client'; // Needs 'use client' because useActionState is a React hook.

import { useActionState } from 'react';
import { signIn, type SignInState } from './actions';

const initialState: SignInState = {};

export function LoginForm({ next }: { next: string }) {
  const [state, formAction, pending] = useActionState(signIn, initialState);

  return (
    <form action={formAction} className="mt-4 space-y-3">
      {/* where to go after signing in (checked again on the server) */}
      <input type="hidden" name="next" value={next} />
      <div>
        <label htmlFor="email" className="block text-sm">Email</label>
        {/* autoComplete="username" so a password manager recognises the form. Paste is never blocked. */}
        <input
          id="email"
          name="email"
          type="email"
          autoComplete="username"
          required
          defaultValue={state.email ?? ''}
          className="w-full rounded border p-2"
        />
      </div>
      <div>
        <label htmlFor="password" className="block text-sm">Password</label>
        <input
          id="password"
          name="password"
          type="password"
          autoComplete="current-password"
          required
          className="w-full rounded border p-2"
        />
      </div>
      <button type="submit" disabled={pending} className="w-full rounded bg-black p-2 text-white disabled:opacity-50">
        {pending ? 'Signing in…' : 'Sign in'}
      </button>
      {state.error && (
        <p role="alert" className="text-sm text-red-600">
          {state.error}
          {state.correlationId && <span> (Reference: {state.correlationId})</span>}
        </p>
      )}
    </form>
  );
}