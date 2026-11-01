// "next" comes from the URL, so it is user input. Only a path on OUR site is followed.
export const DEFAULT_AFTER_LOGIN = '/dashboard/member';

export function safeNextPath(value: string | null | undefined): string {
  if (!value) return DEFAULT_AFTER_LOGIN;
  if (!value.startsWith('/')) return DEFAULT_AFTER_LOGIN; // not a path (https://..., javascript:...)
  if (value.startsWith('//')) return DEFAULT_AFTER_LOGIN; // //evil.example
  if (value.includes('\\')) return DEFAULT_AFTER_LOGIN; // /\evil.example (browsers treat \ like /)
  if (/[\u0000-\u001f]/.test(value)) return DEFAULT_AFTER_LOGIN; // tabs / new lines
  try {
    if (new URL(value, 'http://localhost').origin !== 'http://localhost') return DEFAULT_AFTER_LOGIN;
  } catch {
    return DEFAULT_AFTER_LOGIN;
  }
  return value;
}