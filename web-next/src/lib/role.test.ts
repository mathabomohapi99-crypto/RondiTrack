import { describe, expect, it } from 'vitest';
import { canCheckProofs, isOfficer } from './roles';

describe('isOfficer', () => {
  it.each([['Treasurer', true], ['Admin', true], ['Member', false], [null, false], [undefined, false]] as const)(
    '%s -> %s', (role, expected) => {
      expect(isOfficer(role)).toBe(expected);
      expect(canCheckProofs(role)).toBe(expected);
    });
});