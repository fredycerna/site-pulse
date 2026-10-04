import { ParamMap } from '@angular/router';
import { EVENT_TYPES, EventType } from '../api/baseline-api';

/** The dashboard filters. The URL query params are their only source of truth. */
export interface Filters {
  /** null: the first account in the list. */
  accountId: number | null;
  eventType: EventType;
  /** undefined: the last complete week, chosen by the API. */
  week: string | undefined;
}

/** Invalid or missing values fall back to the defaults instead of breaking the view. */
export function parseFilters(params: ParamMap): Filters {
  const account = params.get('account');
  const type = params.get('type');
  const week = params.get('week');
  return {
    accountId: account !== null && /^\d{1,9}$/.test(account) ? Number(account) : null,
    eventType: EVENT_TYPES.find(t => t === type) ?? 'all',
    week: week !== null && /^\d{4}-\d{2}-\d{2}$/.test(week) ? week : undefined
  };
}
