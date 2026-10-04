import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AccountSummary } from './accounts-api';

/** Event types the API accepts; `all` is the default. */
export const EVENT_TYPES = ['all', 'call_received', 'lead_created', 'appointment_set'] as const;
export type EventType = (typeof EVENT_TYPES)[number];

/** Computed by the API. The view only renders it. */
export type BaselineStatus = 'below' | 'typical' | 'above' | 'insufficient_history' | 'no_data';

/** Local dates are ISO `YYYY-MM-DD` in the account's timezone. */
export interface WeekCount {
  weekStart: string;
  count: number;
}

/** One of the 9 weeks shown; `inBaseline` marks the weeks that counted as history (as many as `historyWeeks`). */
export interface HistoryWeek extends WeekCount {
  inBaseline: boolean;
}

export interface DateRange {
  start: string;
  /** Exclusive: the local Monday after the last day of the range. */
  end: string;
}

/** `median` and the range are null when there is no range: `no_data` or `insufficient_history`. */
export interface MetricBaseline {
  eventType: EventType;
  current: number;
  median: number | null;
  typicalLow: number | null;
  typicalHigh: number | null;
  status: BaselineStatus;
  /** Baseline weeks (0–8) that count as history; fewer than 4 is `insufficient_history`. */
  historyWeeks: number;
  /** 8 baseline weeks, then the selected week last. */
  history: HistoryWeek[];
  /** Baseline weeks outside the range: kept in the history, they do not move it. */
  unusualBaselineWeeks: WeekCount[];
}

export interface LocationBaseline {
  location: string;
  current: number;
  median: number | null;
  typicalLow: number | null;
  typicalHigh: number | null;
  status: BaselineStatus;
  /** Baseline weeks (0–8) that count as history; fewer than 4 is `insufficient_history`. */
  historyWeeks: number;
  deviation: number;
  history: HistoryWeek[];
  /** Baseline weeks outside the location's range: kept in its history, they do not move it. */
  unusualBaselineWeeks: WeekCount[];
}

export interface ActivityBaseline {
  account: AccountSummary;
  week: DateRange;
  baseline: DateRange & { weeks: number };
  eventType: EventType;
  nowUtc: string;
  availableWeeks: string[];
  metrics: MetricBaseline[];
  /** Already sorted by the API: out of range first. */
  locations: LocationBaseline[];
  /** Duplicates ignored inside the 9 weeks shown (the selected week and its baseline). */
  dataNotes: { duplicateRowsIgnored: number };
}

export interface BaselineQuery {
  accountId: number;
  eventType: EventType;
  /** Local Monday; omitted means the last complete week. */
  week?: string;
}

@Injectable({ providedIn: 'root' })
export class BaselineApi {
  private readonly http = inject(HttpClient);

  get(query: BaselineQuery): Observable<ActivityBaseline> {
    let params = new HttpParams().set('eventType', query.eventType);
    if (query.week) {
      params = params.set('week', query.week);
    }
    return this.http.get<ActivityBaseline>(`/api/accounts/${query.accountId}/activity-baseline`, { params });
  }
}
