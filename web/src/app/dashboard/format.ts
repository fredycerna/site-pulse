import { BaselineStatus, EventType } from '../api/baseline-api';

// Presentation only: labels and dates. Status, ranges and order come from the API.

export const METRIC_LABELS: Record<EventType, string> = {
  all: 'All activity',
  call_received: 'Calls',
  lead_created: 'Leads',
  appointment_set: 'Appointments'
};

const NOUNS: Record<EventType, [singular: string, plural: string]> = {
  all: ['event', 'events'],
  call_received: ['call', 'calls'],
  lead_created: ['lead', 'leads'],
  appointment_set: ['appointment', 'appointments']
};

export function countOf(count: number, type: EventType): string {
  return `${count} ${NOUNS[type][count === 1 ? 0 : 1]}`;
}

export function statusLabel(status: BaselineStatus): string {
  switch (status) {
    case 'above':
      return 'Above normal';
    case 'below':
      return 'Below normal';
    case 'typical':
      return 'Normal';
    case 'insufficient_history':
      return 'No baseline yet';
    case 'no_data':
      return 'No activity';
  }
}

/** "within", "above" or "below", to complete "… your normal range". */
export function rangeWord(status: BaselineStatus): string {
  return status === 'above' || status === 'below' ? status : 'within';
}

export function isOutOfRange(status: BaselineStatus): boolean {
  return status === 'above' || status === 'below';
}

/** Only these statuses come with a median and a normal range. */
export function hasRange(status: BaselineStatus): boolean {
  return status === 'typical' || isOutOfRange(status);
}

/** Local ISO dates are calendar days: format them at noon UTC so no timezone can shift the day. */
export function formatDay(iso: string, withYear = false): string {
  return new Date(`${iso}T12:00:00Z`).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    ...(withYear ? { year: 'numeric' } : {}),
    timeZone: 'UTC'
  });
}

export function addDays(iso: string, days: number): string {
  const d = new Date(`${iso}T12:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}

/** `end` is exclusive (the next Monday), so the label ends the day before. */
export function rangeLabel(start: string, endExclusive: string, withYear = false): string {
  return `${formatDay(start)} – ${formatDay(addDays(endExclusive, -1), withYear)}`;
}

export function signedDiff(current: number, median: number | null): string {
  if (median === null) return '';
  const diff = current - median;
  return `${diff >= 0 ? '+' : '−'}${Math.abs(diff)}`;
}

/** "Mon Jul 27, 2026" for an instant, read in UTC. */
export function formatClock(utc: string): string {
  return new Date(utc).toLocaleDateString('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    timeZone: 'UTC'
  });
}
