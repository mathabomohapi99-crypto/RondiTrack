import { describe, expect, it } from 'vitest';
import { chooseKey } from './idempotencyKey';

const K = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const NEW = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
const make = () => NEW;

describe('chooseKey', () => {
  it('reuses the key before it was ever used', () => expect(chooseKey(K, '', 'f1', make)).toBe(K));
  it('reuses the key for the same member and amount (a retry)', () => expect(chooseKey(K, 'f1', 'f1', make)).toBe(K));
  it('makes a new key when the amount or member changed', () => expect(chooseKey(K, 'f1', 'f2', make)).toBe(NEW));
  it('makes a new key when the sent key is not a UUID', () => expect(chooseKey('junk', '', 'f1', make)).toBe(NEW));
});