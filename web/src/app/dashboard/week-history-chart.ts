import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { BaselineStatus, WeekCount } from '../api/baseline-api';
import { formatDay } from './format';

/** The 8 baseline weeks plus the selected one, over the shaded normal range and the median. */
@Component({
  selector: 'app-week-history-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg [attr.viewBox]="'0 0 ' + W + ' ' + H" role="img" [attr.aria-label]="label()">
      <rect x="24" [attr.y]="y(high())" [attr.width]="W - 34" [attr.height]="y(low()) - y(high())" class="band" />
      <line x1="24" [attr.x2]="W - 10" [attr.y1]="y(median())" [attr.y2]="y(median())" class="median" />
      <line x1="24" [attr.x2]="W - 10" [attr.y1]="BASE" [attr.y2]="BASE" class="axis" />
      @for (bar of bars(); track bar.weekStart) {
        <rect [attr.x]="bar.x" [attr.y]="y(bar.count)" [attr.width]="BW" [attr.height]="BASE - y(bar.count)" rx="2"
              [attr.class]="bar.last ? 'bar current ' + status() : 'bar'" />
        <text [attr.x]="bar.x + BW / 2" [attr.y]="y(bar.count) - 3" class="value">{{ bar.count }}</text>
        <text [attr.x]="bar.x + BW / 2" [attr.y]="BASE + 14" class="week">{{ bar.label }}</text>
      }
      @if (bars().length) {
        <text [attr.x]="bars()[bars().length - 1].x + BW / 2" [attr.y]="BASE + 26" class="this-week">this week</text>
      }
    </svg>
  `,
  styles: `
    :host { display: block; }
    svg { display: block; width: 100%; max-height: 180px; }
    .band { fill: var(--band); opacity: 0.6; }
    .median { stroke: var(--ink); stroke-dasharray: 4 3; }
    .axis { stroke: var(--line); }
    .bar { fill: #a9b0ba; }
    .bar.current { fill: var(--dot); }
    .bar.current.above { fill: var(--above); }
    .bar.current.below { fill: var(--below); }
    text { text-anchor: middle; }
    .value { font-size: 11px; fill: var(--ink); }
    .week { font-size: 10px; fill: var(--muted); }
    .this-week { font-size: 10px; font-weight: 600; fill: var(--ink); }
  `
})
export class WeekHistoryChart {
  readonly history = input.required<WeekCount[]>();
  readonly median = input.required<number>();
  readonly low = input.required<number>();
  readonly high = input.required<number>();
  readonly status = input.required<BaselineStatus>();

  protected readonly W = 500;
  protected readonly H = 150;
  protected readonly TOP = 14;
  protected readonly BASE = 122;
  protected readonly BW = 34;

  private readonly max = computed(() => Math.max(this.high(), 1, ...this.history().map(h => h.count)) * 1.1);

  protected readonly bars = computed(() => {
    const n = this.history().length;
    const gap = n > 1 ? (this.W - 40 - this.BW * n) / (n - 1) : 0;
    return this.history().map((h, i) => ({
      weekStart: h.weekStart,
      count: h.count,
      label: formatDay(h.weekStart),
      x: 30 + i * (this.BW + gap),
      last: i === n - 1
    }));
  });

  protected readonly label = computed(
    () => `Last ${this.history().length} weeks: ${this.history().map(h => `${formatDay(h.weekStart)} ${h.count}`).join(', ')}`
  );

  protected y(value: number): number {
    return this.BASE - (value / this.max()) * (this.BASE - this.TOP);
  }
}
