const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

// Same member + amount as the attempt that made the key (or no attempt yet) = REUSE it (a retry). Anything else = a NEW key.
export function chooseKey(
  sentKey: string,
  keyFor: string,
  fingerprint: string,
  make: () => string = () => crypto.randomUUID(),
): string {
  const reusable = UUID.test(sentKey) && (keyFor === '' || keyFor === fingerprint);
  return reusable ? sentKey : make();
}