'use client'; // Needs 'use client' because useActionState is a React hook.

import Link from 'next/link';
import { useActionState } from 'react';
import { register, type RegisterState } from './actions';

const initialState: RegisterState = {};

export function RegisterForm() {
  const [state, formAction, pending] = useActionState(register, initialState);

  return (
    <form action={formAction} className="mt-4 space-y-3">
      <div>
        <label htmlFor="fullName" className="block text-sm">Full name</label>
        <input id="fullName" name="fullName" autoComplete="name" required maxLength={100}
          defaultValue={state.values?.fullName ?? ''} className="w-full rounded border p-2" />
        {state.fieldErrors?.fullname && <p role="alert" className="text-sm text-red-600">{state.fieldErrors.fullname}</p>}
      </div>
      <div>
        <label htmlFor="email" className="block text-sm">Email</label>
        <input id="email" name="email" type="email" autoComplete="username" required
          defaultValue={state.values?.email ?? ''} className="w-full rounded border p-2" />
        {state.fieldErrors?.email && <p role="alert" className="text-sm text-red-600">{state.fieldErrors.email}</p>}
      </div>
      <div>
        <label htmlFor="password" className="block text-sm">Password (at least 12 characters)</label>
        {/* new-password lets a password manager offer a strong one. Paste is never blocked. */}
        <input id="password" name="password" type="password" autoComplete="new-password" required minLength={12}
          className="w-full rounded border p-2" />
        {state.fieldErrors?.password && <p role="alert" className="text-sm text-red-600">{state.fieldErrors.password}</p>}
      </div>
      <button type="submit" disabled={pending} className="w-full rounded bg-black p-2 text-white disabled:opacity-50">
        {pending ? 'Creating account…' : 'Create account'}
      </button>
      {state.error && (
        <p role="alert" className="text-sm text-red-600">
          {state.error}
          {state.correlationId && <span> (Reference: {state.correlationId})</span>}
        </p>
      )}
      <p className="text-sm">
        Already have an account? <Link href="/login" className="underline">Sign in</Link>
      </p>
    </form>
  );
}