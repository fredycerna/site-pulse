import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { AccountSummary, AccountsApi } from '../api/accounts-api';
import { ActivityBaseline, BaselineApi, BaselineQuery, EVENT_TYPES, EventType } from '../api/baseline-api';
import {
  METRIC_LABELS,
  addDays,
  countOf,
  formatClock,
  formatDay,
  isOutOfRange,
  rangeLabel,
  rangeWord,
  signedDiff,
  statusLabel
} from './format';
import { RangeBar } from './range-bar';
import { WeekHistoryChart } from './week-history-chart';

type Load<T> = { kind: 'loading' } | { kind: 'error'; message: string } | { kind: 'ready'; data: T };

/** Minimum full weeks before a baseline exists (PLAN.md §5). */
const MIN_HISTORY_WEEKS = 4;

@Component({
  selector: 'app-dashboard',
  imports: [RangeBar, WeekHistoryChart],
  templateUrl: './dashboard.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Dashboard {
  private readonly accountsApi = inject(AccountsApi);
  private readonly baselineApi = inject(BaselineApi);

  protected readonly eventTypes = EVENT_TYPES;
  protected readonly metricLabels = METRIC_LABELS;
  protected readonly statusLabel = statusLabel;
  protected readonly rangeWord = rangeWord;
  protected readonly signedDiff = signedDiff;
  protected readonly countOf = countOf;
  protected readonly formatDay = formatDay;
  protected readonly isOutOfRange = isOutOfRange;

  // Filters.
  readonly accountId = signal<number | null>(null);
  readonly eventType = signal<EventType>('all');
  readonly week = signal<string | undefined>(undefined);

  protected readonly accounts = toSignal(
    this.accountsApi.list().pipe(
      map((data): Load<AccountSummary[]> => ({ kind: 'ready', data })),
      catchError(() => of<Load<AccountSummary[]>>({ kind: 'error', message: 'Could not load accounts. Is the API running?' }))
    ),
    { initialValue: { kind: 'loading' } as Load<AccountSummary[]> }
  );

  /** The account shown: the selected one, or the first in the list. */
  protected readonly selectedAccountId = computed(() => {
    const accounts = this.accounts();
    if (accounts.kind !== 'ready' || accounts.data.length === 0) return null;
    const id = this.accountId();
    return accounts.data.some(a => a.id === id) ? id : accounts.data[0].id;
  });

  private readonly query = computed<BaselineQuery | null>(() => {
    const accountId = this.selectedAccountId();
    return accountId === null ? null : { accountId, eventType: this.eventType(), week: this.week() };
  });

  protected readonly baseline = toSignal(
    toObservable(this.query).pipe(
      switchMap(query =>
        query === null
          ? of<Load<ActivityBaseline> | null>(null)
          : this.baselineApi.get(query).pipe(
              map((data): Load<ActivityBaseline> => ({ kind: 'ready', data })),
              catchError((err: unknown) => of<Load<ActivityBaseline>>({ kind: 'error', message: errorMessage(err) })),
              startWith<Load<ActivityBaseline>>({ kind: 'loading' })
            )
      )
    ),
    { initialValue: null }
  );

  protected readonly data = computed(() => {
    const b = this.baseline();
    return b?.kind === 'ready' ? b.data : null;
  });

  protected readonly selectedMetric = computed(() => {
    const data = this.data();
    return data?.metrics.find(m => m.eventType === data.eventType) ?? null;
  });

  protected readonly isEmptyAccount = computed(() => this.data()?.metrics.every(m => m.status === 'no_data') ?? false);

  protected readonly outOfRangeCount = computed(() => this.data()?.locations.filter(l => isOutOfRange(l.status)).length ?? 0);

  protected readonly weekLabel = computed(() => {
    const data = this.data();
    return data ? rangeLabel(data.week.start, data.week.end, true) : '';
  });

  protected readonly baselineLabel = computed(() => {
    const data = this.data();
    return data ? rangeLabel(data.baseline.start, data.baseline.end) : '';
  });

  protected readonly clockLabel = computed(() => {
    const data = this.data();
    return data ? formatClock(data.nowUtc) : '';
  });

  /** Full weeks still missing before a baseline exists: weeks with data before the selected one come from `availableWeeks`. */
  protected readonly weeksToGo = computed(() => {
    const data = this.data();
    if (!data) return 0;
    const before = data.availableWeeks.indexOf(data.week.start);
    return Math.max(1, MIN_HISTORY_WEEKS - Math.max(before, 0));
  });

  /** Newest first; the last available week is the default and is labeled as such. */
  protected readonly weekOptions = computed(() => {
    const weeks = this.data()?.availableWeeks ?? [];
    const last = weeks[weeks.length - 1];
    return [...weeks].reverse().map(start => ({
      start,
      label: `${rangeLabel(start, addDays(start, 7), true)}${start === last ? ' · last complete' : ''}`
    }));
  });

  /** The week after the last complete one is in progress and never compared. */
  protected readonly inProgressWeek = computed(() => {
    const weeks = this.data()?.availableWeeks ?? [];
    return weeks.length ? addDays(weeks[weeks.length - 1], 7) : null;
  });

  /** One scale for all location rows so their bars are comparable. */
  protected readonly locationScale = computed(
    () => Math.max(1, ...(this.data()?.locations ?? []).map(l => Math.max(l.typicalHigh, l.current))) * 1.1
  );

  /** The open location detail. Resets with new data to the first location when it is out of range, as in the mockup. */
  protected readonly expanded = linkedSignal<string | null>(() => {
    const first = this.data()?.locations[0];
    return first && isOutOfRange(first.status) ? first.location : null;
  });

  protected selectAccount(value: string): void {
    this.accountId.set(Number(value));
    this.week.set(undefined);
  }

  protected selectType(type: EventType): void {
    this.eventType.set(type);
  }

  protected selectWeek(value: string): void {
    const weeks = this.data()?.availableWeeks ?? [];
    this.week.set(value === weeks[weeks.length - 1] ? undefined : value);
  }

  protected toggle(location: string): void {
    this.expanded.update(current => (current === location ? null : location));
  }

  protected cardScale(current: number, high: number): number {
    return Math.max(1, high, current) * 1.25;
  }
}

function errorMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const problem = err.error as { title?: string; detail?: string } | null;
    if (err.status === 404) return 'This account does not exist.';
    if (err.status === 0) return 'Could not reach the API. Is it running?';
    const text = problem?.detail ?? problem?.title;
    if (err.status >= 400 && err.status < 500 && text) return text;
  }
  return 'Something went wrong while loading this week. Try again in a moment.';
}
