import type { Role } from './types';

// Treasurer or Admin OF THIS STOKVEL (the API's answer for this stokvel, never the token)
export const isOfficer = (role: Role | null | undefined): boolean => role === 'Treasurer' || role === 'Admin';
export const canCheckProofs = isOfficer; // treasurers open every member's proof; members only their own