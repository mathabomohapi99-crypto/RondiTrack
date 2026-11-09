import { describe, expect, it } from 'vitest';
import { joinHref, normalizeCode, parseScreen, transition } from './joinFlow';

describe('join flow', () => {
  it('moves forward and back through the three steps', () => {
    expect(transition('code', 'codeAccepted')).toBe('rules');
    expect(transition('rules', 'rulesRead')).toBe('confirm');
    expect(transition('confirm', 'back')).toBe('rules');
    expect(transition('rules', 'back')).toBe('code');
  });
  it('ignores an event that makes no sense for a step', () => {
    expect(transition('code', 'rulesRead')).toBe('code');
  });
  it('already a member leaves the steps', () => {
    expect(transition('confirm', 'alreadyMember')).toBe('member');
  });
  it('cleans up a pasted code and refuses a wrong length', () => {
    expect(normalizeCode('k7m2 9qxp')).toBe('K7M29QXP');
    expect(normalizeCode('K7M2-9QXP')).toBe('K7M29QXP');
    expect(normalizeCode('K7M2')).toBeNull();
    expect(normalizeCode('')).toBeNull();
  });
  it('an unknown step in the URL becomes step 1', () => {
    expect(parseScreen('banana')).toBe('code');
    expect(parseScreen(undefined)).toBe('code');
    expect(parseScreen('confirm')).toBe('confirm');
  });
  it('builds URLs that name their screen', () => {
    expect(joinHref('rules', 'K7M29QXP')).toBe('/join?code=K7M29QXP&step=rules');
  });
});