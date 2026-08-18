import { describeDaysToExpiry, formatIsoDate, formatMoney } from './format';

describe('formatIsoDate', () => {
  it('renders a date-only string without shifting it across a time zone', () => {
    // The naive implementation (new Date('2026-09-05')) parses as midnight UTC and would
    // render "04 Sep 2026" anywhere west of Greenwich. This is the regression guard.
    expect(formatIsoDate('2026-09-05')).toBe('05 Sep 2026');
    expect(formatIsoDate('2026-01-01')).toBe('01 Jan 2026');
    expect(formatIsoDate('2026-12-31')).toBe('31 Dec 2026');
  });

  it('tolerates a full timestamp by using its date part', () => {
    expect(formatIsoDate('2026-08-18T09:41:12Z')).toBe('18 Aug 2026');
  });

  it('falls back to a dash when there is no date', () => {
    expect(formatIsoDate(null)).toBe('-');
    expect(formatIsoDate(undefined)).toBe('-');
  });
});

describe('formatMoney', () => {
  it('always shows exactly two decimal places', () => {
    expect(formatMoney(3.4)).toBe('3.40');
    expect(formatMoney(12.5)).toBe('12.50');
    expect(formatMoney(0)).toBe('0.00');
    expect(formatMoney(2450)).toBe('2,450.00');
  });

  it('treats a missing price as zero rather than NaN', () => {
    expect(formatMoney(null)).toBe('0.00');
  });
});

describe('describeDaysToExpiry', () => {
  it('reads naturally on both sides of today', () => {
    expect(describeDaysToExpiry(-6)).toBe('expired 6 days ago');
    expect(describeDaysToExpiry(-1)).toBe('expired 1 day ago');
    expect(describeDaysToExpiry(0)).toBe('expires today');
    expect(describeDaysToExpiry(1)).toBe('in 1 day');
    expect(describeDaysToExpiry(29)).toBe('in 29 days');
  });
});
