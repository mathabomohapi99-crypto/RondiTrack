// Every key in one place. Data for one stokvel ALWAYS starts with ['stokvels', id],
// so invalidateQueries({ queryKey: queryKeys.stokvel(id) }) refreshes one stokvel only.
export const queryKeys = {
  home: ['me', 'home'] as const,
  stokvels: ['stokvels'] as const,
  stokvel: (id: string) => ['stokvels', id] as const,
  cycles: (id: string) => ['stokvels', id, 'cycles'] as const,
  // Changed: contributions belong to a cycle in my API, so the cycle id is in the key too
  contributions: (id: string, cycleId: string) =>
    ['stokvels', id, 'cycles', cycleId, 'contributions'] as const,
  // NEW 7.4: the member list (a Treasurer picks from it)
  members: (id: string) => ['stokvels', id, 'members'] as const,
};