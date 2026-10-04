import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { BaselineStatus } from '../api/baseline-api';

/** Band = normal range, tick = median, dot = this week. Draws what the API computed. */
@Component({
  selector: 'app-range-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg role="img" [attr.aria-label]="label()">
      <rect x="0" y="9" width="100%" height="8" rx="4" class="track" />
      <rect [attr.x]="pct(low())" y="7" [attr.width]="bandWidth()" height="12" rx="4" class="band" />
      <line [attr.x1]="pct(median())" [attr.x2]="pct(median())" y1="4" y2="22" class="median" />
      <circle [attr.cx]="pct(current())" cy="13" r="6" [attr.class]="'dot ' + status()" />
    </svg>
  `,
  styles: `
    /* Fixed height and percentage x: the bar is equally thick in a card and in a row. */
    :host { display: block; padding: 0 8px; }
    svg { display: block; width: 100%; height: 26px; overflow: visible; }
    .track { fill: var(--track); }
    .band { fill: var(--band); }
    .median { stroke: var(--ink); stroke-width: 2; }
    .dot { fill: var(--dot); stroke: #fff; stroke-width: 2; }
    .dot.above { fill: var(--above); }
    .dot.below { fill: var(--below); }
  `
})
export class RangeBar {
  readonly current = input.required<number>();
  readonly median = input.required<number>();
  readonly low = input.required<number>();
  readonly high = input.required<number>();
  readonly status = input.required<BaselineStatus>();
  /** Scale maximum, shared by rows that should be comparable. */
  readonly max = input.required<number>();

  protected readonly label = computed(() => `${this.current()}, normal ${this.low()} to ${this.high()}, median ${this.median()}`);
  protected readonly bandWidth = computed(() => `${Math.max(1, this.ratio(this.high()) - this.ratio(this.low()))}%`);

  protected pct(value: number): string {
    return `${this.ratio(value)}%`;
  }

  private ratio(value: number): number {
    const max = Math.max(this.max(), 1);
    return (Math.min(Math.max(value, 0), max) / max) * 100;
  }
}
