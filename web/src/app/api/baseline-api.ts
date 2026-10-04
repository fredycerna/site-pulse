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

export interface DateRange {
  start: string;
  /** Exclusive: the local Monday after the last day of the range. */
  end: string;
}

export interface MetricBaseline {
  eventType: EventType;
  current: number;
  median: number;
  typicalLow: number;
  typicalHigh: number;
  status: BaselineStatus;
  /** 8 baseline weeks, then the selected week last. */
  history: WeekCount[];
  /** Baseline weeks outside the range: kept in the history, they do not move it. */
  unusualBaselineWeeks: WeekCount[];
}

export interface LocationBaseline {
  location: string;
  current: number;
  median: number;
  typicalLow: number;
  typicalHigh: number;
  status: BaselineStatus;
  deviation: number;
  history: WeekCount[];
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
