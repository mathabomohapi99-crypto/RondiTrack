import { describe, expect, it } from 'vitest';
import { isRealCalendarDate, southAfricanDaysToUtcRange } from './saDates';

// Is an instant inside the half-open range [from, to)?
const inside = (instant: string, range: { from?: string; to?: string }) => {
  const t = Date.parse(instant);
  return (range.from === undefined || t >= Date.parse(range.from)) && (range.to === undefined || t < Date.parse(range.to));
};

describe('southAfricanDaysToUtcRange', () => {
  it('turns 1 to 31 October into [30 Sep 22:00Z, 31 Oct 22:00Z)', () => {
    expect(southAfricanDaysToUtcRange('2026-10-01', '2026-10-31')).toEqual({
      from: '2026-09-30T22:00:00Z', // included
      to: '2026-10-31T22:00:00Z', // excluded
    });
  });

  it('keeps 23:30 SAST on 31 October inside and 00:00 SAST on 1 November outside', () => {
    const range = southAfricanDaysToUtcRange('2026-10-01', '2026-10-31');
    expect(inside('2026-10-31T21:30:00Z', range)).toBe(true); // Thandi, 23:30 SAST
    expect(inside('2026-10-31T22:00:00Z', range)).toBe(false); // 00:00 SAST on 1 Nov
    expect(inside('2026-09-30T22:00:00Z', range)).toBe(true); // 00:00 SAST on 1 Oct
    expect(inside('2026-09-30T21:59:59Z', range)).toBe(false); // 23:59:59 SAST on 30 Sep
  });

  it('a single day covers exactly 24 hours', () => {
    const range = southAfricanDaysToUtcRange('2026-10-15', '2026-10-15');
    expect(Date.parse(range.to!) - Date.parse(range.from!)).toBe(24 * 60 * 60 * 1000);
  });

  it('works with only a start date, only an end date, or neither', () => {
    expect(southAfricanDaysToUtcRange('2026-10-01', null)).toEqual({ from: '2026-09-30T22:00:00Z' });
    expect(southAfricanDaysToUtcRange(undefined, '2026-10-31')).toEqual({ to: '2026-10-31T22:00:00Z' });
    expect(southAfricanDaysToUtcRange(null, null)).toEqual({});
  });

  it('refuses an end date before the start date (our rule: the page ignores both dates)', () => {
    expect(() => southAfricanDaysToUtcRange('2026-10-31', '2026-10-01')).toThrow(RangeError);
  });

  it('refuses dates that do not exist instead of rolling them over', () => {
    expect(() => southAfricanDaysToUtcRange('2026-02-30', null)).toThrow(RangeError); // would be 2 March
    expect(() => southAfricanDaysToUtcRange(null, '2026-13-01')).toThrow(RangeError);
    expect(() => southAfricanDaysToUtcRange('yesterday', null)).toThrow(RangeError);
  });

  it('handles the end of a year and a leap day', () => {
    expect(southAfricanDaysToUtcRange(null, '2026-12-31')).toEqual({ to: '2026-12-31T22:00:00Z' });
    expect(southAfricanDaysToUtcRange(null, '2028-02-29')).toEqual({ to: '2028-02-29T22:00:00Z' });
    expect(isRealCalendarDate('2028-02-29')).toBe(true);
    expect(isRealCalendarDate('2026-02-29')).toBe(false);
  });
});