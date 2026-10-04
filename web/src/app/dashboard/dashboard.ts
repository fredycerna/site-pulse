import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { ActivatedRoute, Router } from '@angular/router';
import { AccountSummary, AccountsApi } from '../api/accounts-api';
import { ActivityBaseline, BaselineApi, BaselineQuery, EventType, LocationBaseline } from '../api/baseline-api';
import {
  METRIC_LABELS,
  METRIC_VERBS,
  addDays,
  baselineLabel,
  countOf,
  formatClock,
  formatDay,
  hasRange,
  isOutOfRange,
  rangeLabel,
  rangeWord,
  signedDiff,
  statusLabel
} from './format';
import { RangeBar } from './range-bar';
import { parseFilters } from './url-filters';
import { WeekHistoryChart } from './week-history-chart';

/** Full weeks the API needs before a baseline exists (PLAN.md §5). */
const MIN_HISTORY_WEEKS = 4;

type Load<T> = { kind: 'loading' } | { kind: 'error'; message: string } | { kind: 'ready'; data: T };

@Component({
  selector: 'app-dashboard',
  imports: [RangeBar, WeekHistoryChart],
  templateUrl: './dashboard.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Dashboard {
  private readonly accountsApi = inject(AccountsApi);
  private readonly baselineApi = inject(BaselineApi);

  protected readonly metricLabels = METRIC_LABELS;
  protected readonly metricVerbs = METRIC_VERBS;
  protected readonly statusLabel = statusLabel;
  protected readonly rangeWord = rangeWord;
  protected readonly signedDiff = signedDiff;
  protected readonly countOf = countOf;
  protected readonly formatDay = formatDay;
  protected readonly isOutOfRange = isOutOfRange;
  protected readonly hasRange = hasRange;

  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  /** Filters come only from the URL, so a reload or a pasted link restores the view. */
  private readonly filters = toSignal(this.route.queryParamMap.pipe(map(parseFilters)), {
    initialValue: parseFilters(this.route.snapshot.queryParamMap)
  });
  protected readonly accountId = computed(() => this.filters().accountId);
  protected readonly eventType = computed(() => this.filters().eventType);
  protected readonly week = computed(() => this.filters().week);

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

  /** Full weeks still missing before the selected metric has a baseline; `historyWeeks` comes from the API. */
  protected readonly weeksToGo = computed(() => {
    const metric = this.selectedMetric();
    return metric ? Math.max(0, MIN_HISTORY_WEEKS - metric.historyWeeks) : 0;
  });

  protected readonly isEmptyAccount = computed(() => this.data()?.metrics.every(m => m.status === 'no_data') ?? false);

  protected readonly outOfRangeCount = computed(() => this.data()?.locations.filter(l => isOutOfRange(l.status)).length ?? 0);

  protected readonly weekLabel = computed(() => {
    const data = this.data();
    return data ? rangeLabel(data.week.start, data.week.end, true) : '';
  });

  /** The weeks the selected metric really used, as marked by the API. */
  protected readonly baselineLabel = computed(() => {
    const data = this.data();
    const metric = this.selectedMetric();
    return data && metric ? baselineLabel(metric.history, data.baseline.end) : '';
  });

  protected locationBaselineLabel(loc: LocationBaseline, endExclusive: string): string {
    return baselineLabel(loc.history, endExclusive);
  }

  protected readonly clockLabel = computed(() => {
    const data = this.data();
    return data ? formatClock(data.nowUtc) : '';
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
    () => Math.max(1, ...(this.data()?.locations ?? []).map(l => Math.max(l.typicalHigh ?? 0, l.current))) * 1.1
  );

  /** The open location detail. Resets with new data to the first location when it is out of range, as in the mockup. */
  protected readonly expanded = linkedSignal<string | null>(() => {
    const first = this.data()?.locations[0];
    return first && isOutOfRange(first.status) ? first.location : null;
  });

  protected selectAccount(value: string): void {
    this.updateUrl({ account: value, week: null });
  }

  protected selectType(type: EventType): void {
    this.updateUrl({ type: type === 'all' ? null : type });
  }

  protected selectWeek(value: string | null): void {
    const weeks = this.data()?.availableWeeks ?? [];
    this.updateUrl({ week: value === weeks[weeks.length - 1] ? null : value });
  }

  /** Defaults are left out of the URL (null removes the param). */
  private updateUrl(queryParams: Record<string, string | null>): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams, queryParamsHandling: 'merge' });
  }

  protected toggle(location: string): void {
    this.expanded.update(current => (current === location ? null : location));
  }

  protected cardScale(current: number, high: number | null): number {
    return Math.max(1, high ?? 0, current) * 1.25;
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
