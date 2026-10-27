'use client'; // Needs 'use client' because useActionState is a React hook.

import { useActionState } from 'react';
import { signIn, type SignInState } from './actions';

const initialState: SignInState = {};

export function LoginForm() {
  const [state, formAction, pending] = useActionState(signIn, initialState);

  return (
    <form action={formAction} className="mt-4 space-y-3">
      <input name="email" type="email" placeholder="Email" required className="w-full rounded border p-2" />
      <input name="password" type="password" placeholder="Password" required className="w-full rounded border p-2" />
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