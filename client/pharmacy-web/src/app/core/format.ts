/**
 * Small formatting helpers.
 *
 * Dates from the API are date-only strings (yyyy-MM-dd). They are formatted by splitting
 * the string rather than by passing it through `new Date(...)`, because the JavaScript
 * Date constructor reads a bare yyyy-MM-dd as midnight UTC - which renders as the
 * *previous* day for anyone west of Greenwich. An expiry date that silently shifts by a
 * day is exactly the kind of bug that matters in a pharmacy.
 */

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** 2026-09-05 -> "05 Sep 2026". */
export function formatIsoDate(iso: string | null | undefined): string {
  if (!iso) {
    return '-';
  }

  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso);
  if (!match) {
    return iso;
  }

  const [, year, month, day] = match;
  const monthName = MONTHS[Number(month) - 1] ?? month;
  return `${day} ${monthName} ${year}`;
}

/** A full UTC timestamp rendered in the browser's local time. */
export function formatTimestamp(iso: string | null | undefined): string {
  if (!iso) {
    return '-';
  }

  const value = new Date(iso);
  if (Number.isNaN(value.getTime())) {
    return iso;
  }

  return `${formatIsoDate(toLocalIsoDate(value))} ${value
    .getHours()
    .toString()
    .padStart(2, '0')}:${value.getMinutes().toString().padStart(2, '0')}`;
}

function toLocalIsoDate(value: Date): string {
  const year = value.getFullYear();
  const month = (value.getMonth() + 1).toString().padStart(2, '0');
  const day = value.getDate().toString().padStart(2, '0');
  return `${year}-${month}-${day}`;
}

const MONEY = new Intl.NumberFormat('en-US', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

/** Prices are always shown with exactly two decimals, as the brief requires. */
export function formatMoney(value: number | null | undefined): string {
  return MONEY.format(value ?? 0);
}

/** Today as yyyy-MM-dd in the browser's local time - the min for a new expiry date. */
export function todayIso(): string {
  return toLocalIsoDate(new Date());
}

/** Plain-English gloss of the days-to-expiry number shown under the date. */
export function describeDaysToExpiry(days: number): string {
  if (days < 0) {
    return `expired ${Math.abs(days)} day${Math.abs(days) === 1 ? '' : 's'} ago`;
  }

  if (days === 0) {
    return 'expires today';
  }

  return `in ${days} day${days === 1 ? '' : 's'}`;
}
