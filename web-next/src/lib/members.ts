import 'server-only';
import { apiJson } from './api';
import { ApiError } from './errors';
import type { MemberOption } from './types';

// The stokvel's members as { id, name } only (no emails). A failure here must not break the page, so it gives [].
export async function fetchMembers(stokvelId: string): Promise<MemberOption[]> {
  try {
    const page = await apiJson<{ items: { id: string; fullName?: string; name?: string }[] }>(
      `/stokvels/${stokvelId}/members?pageSize=100`,
    );
    return page.items.map((u) => ({ id: u.id, name: u.fullName ?? u.name ?? 'Member' }));
  } catch (error) {
    if (error instanceof ApiError) return []; // redirects and not-found are NOT ApiErrors, so they still pass through
    throw error;
  }
}