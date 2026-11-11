import { describe, expect, it } from 'vitest';
import { formatDateTime } from './format';
import { saMonthOf } from './saDates';

describe('the South African month of an instant', () => {
  it('23:30 on the last day of October still belongs to October', () => {
    expect(saMonthOf('2026-10-31T21:30:00Z')).toBe('2026-10'); // 23:30 SAST
  });
  it('00:00 SAST on 1 November belongs to November', () => {
    expect(saMonthOf('2026-10-31T22:00:00Z')).toBe('2026-11');
  });
  it('handles the end of a year', () => {
    expect(saMonthOf('2026-12-31T21:59:59Z')).toBe('2026-12');
    expect(saMonthOf('2026-12-31T22:00:00Z')).toBe('2027-01');
  });
  it('refuses text that is not an instant', () => {
    expect(() => saMonthOf('yesterday')).toThrow(RangeError);
  });
});

describe('formatDateTime shows South African time whatever the laptop or CI zone is', () => {
  it('23:30 on 31 October stays on 31 October', () => {
    expect(formatDateTime('2026-10-31T21:30:00Z')).toMatch(/31\s+Oct\s+2026.*23[:.]30/);
  });
  it('22:00 UTC is already 1 November in South Africa', () => {
    expect(formatDateTime('2026-10-31T22:00:00Z')).toMatch(/\b0?1\s+Nov\s+2026.*00[:.]00/);
  });
});