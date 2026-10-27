import type { MyStokvel } from './types';

// Pure filter: keeps only the stokvels where the person is Treasurer.
// The role comes from the API's answer for each stokvel, never from the token or the session.
export function stokvelsWhereTreasurer(list: MyStokvel[]): MyStokvel[] {
  return list.filter((s) => s.myRole === 'Treasurer');
}