import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ActivityBaseline } from '../api/baseline-api';
import { Dashboard } from './dashboard';
import sample from '../api/testing/baseline-account-6.json';

const ACCOUNTS = [
  { id: 6, name: 'Metro Collision Centers', timezone: 'America/New_York' },
  { id: 20, name: 'Quiet Harbor Spa', timezone: 'America/Los_Angeles' }
];

function baseline(): ActivityBaseline {
  return structuredClone(sample) as ActivityBaseline;
}

describe('Dashboard', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Dashboard],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render(data: ActivityBaseline = baseline()) {
    const fixture = TestBed.createComponent(Dashboard);
    http.expectOne('/api/accounts').flush(ACCOUNTS);
    await fixture.whenStable();
    http.expectOne(r => r.url.endsWith('/activity-baseline')).flush(data);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('lists the accounts returned by the API in the account selector', async () => {
    const el = await render();

    const options = el.querySelectorAll('select#account option');
    expect(Array.from(options).map(o => o.textContent?.trim())).toEqual(['Metro Collision Centers', 'Quiet Harbor Spa']);
  });

  it('shows an error when the accounts call fails', async () => {
    const fixture = TestBed.createComponent(Dashboard);
    http.expectOne('/api/accounts').flush('down', { status: 503, statusText: 'Service Unavailable' });
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')).toBeTruthy();
  });

  it('states the verdict with its numbers and what it was compared against', async () => {
    const el = await render();

    expect(el.querySelector('h1')?.textContent?.replace(/\s+/g, ' ').trim()).toBe(
      'All activity is within your normal range (87, normal 24–121). Every location is within its own range.'
    );
    expect(el.querySelector('.sub')?.textContent?.trim()).toBe('Compared with your own 8 previous full weeks (May 25 – Jul 19).');
    expect(el.querySelector('.eyebrow')?.textContent).toContain('Jul 20 – Jul 26, 2026');
    expect(el.querySelector('.eyebrow')?.textContent).toContain('America/New_York');
    expect(el.querySelector('.clock')?.textContent?.trim()).toBe('Demo clock pinned to Mon Jul 27, 2026 (seed snapshot)');
  });

  it('offers the API weeks newest first, labeling the last complete one', async () => {
    const el = await render();

    const options = Array.from(el.querySelectorAll<HTMLOptionElement>('select#week option'));
    expect(options).toHaveLength(25);
    expect(options[0].textContent?.trim()).toBe('Jul 20 – Jul 26, 2026 · last complete');
    expect(options[0].selected).toBe(true);
    expect(options[24].textContent?.trim()).toBe('Feb 2 – Feb 8, 2026');
  });

  it('shows the API error detail when the baseline call fails', async () => {
    const fixture = TestBed.createComponent(Dashboard);
    http.expectOne('/api/accounts').flush(ACCOUNTS);
    await fixture.whenStable();
    http
      .expectOne(r => r.url.endsWith('/activity-baseline'))
      .flush({ title: 'Bad request', detail: 'week must be a Monday.' }, { status: 400, statusText: 'Bad Request' });
    await fixture.whenStable();

    const alert = (fixture.nativeElement as HTMLElement).querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('week must be a Monday.');
  });

  function withoutBaseline(historyWeeks: number): ActivityBaseline {
    const data = baseline();
    data.week = { start: '2026-02-16', end: '2026-02-23' };
    data.metrics[0] = { ...data.metrics[0], status: 'insufficient_history', median: null, typicalLow: null, typicalHigh: null, historyWeeks };
    return data;
  }

  it('says how many full weeks are missing when there is no baseline yet', async () => {
    const el = await render(withoutBaseline(1));

    expect(el.querySelector('h1')?.textContent?.trim()).toBe('No baseline yet');
    expect(el.querySelector('.sub')?.textContent?.replace(/\s+/g, ' ').trim()).toBe(
      'We need at least 4 full weeks to know what is normal for you. 3 weeks to go.'
    );
    expect(el.querySelectorAll('.mcard')[0].querySelector('app-range-bar')).toBeNull();
  });

  it('uses the singular when one week is missing', async () => {
    const el = await render(withoutBaseline(3));

    expect(el.querySelector('.sub')?.textContent).toContain('1 week to go.');
  });

  describe('locations', () => {
    function withOutOfRange(): ActivityBaseline {
      const data = baseline();
      const [a, b, c] = data.locations;
      // The API order is the attention order; the view must keep it, not re-sort.
      data.locations = [
        { ...b, location: 'Site Z', current: 0, status: 'below', deviation: -3.2 },
        { ...a, location: 'Site B', current: 14, status: 'above', deviation: 2.6 },
        { ...c, location: 'Site A', status: 'typical', deviation: 0.4 },
        ...data.locations.slice(3)
      ];
      data.metrics[0] = { ...data.metrics[0], current: 130, status: 'above' };
      return data;
    }

    function rows(el: HTMLElement) {
      return Array.from(el.querySelectorAll<HTMLButtonElement>('button.row')).map(r => ({
        name: r.querySelector('.loc-name')?.textContent?.trim(),
        label: r.querySelector('.pill')?.textContent?.trim(),
        expanded: r.getAttribute('aria-expanded')
      }));
    }

    it('keeps the API attention order and labels out-of-range locations in words', async () => {
      const el = await render(withOutOfRange());

      expect(rows(el).slice(0, 3)).toEqual([
        { name: 'Site Z', label: 'Below normal', expanded: 'true' },
        { name: 'Site B', label: 'Above normal', expanded: 'false' },
        { name: 'Site A', label: 'Normal', expanded: 'false' }
      ]);
      expect(rows(el)).toHaveLength(15);
    });

    it('counts out-of-range locations and words the verdict from the API status', async () => {
      const el = await render(withOutOfRange());

      expect(el.querySelector('h1')?.textContent?.replace(/\s+/g, ' ').trim()).toBe(
        'All activity is above your normal range (130, normal 24–121). 2 locations are outside their own range.'
      );
    });

    it('explains the open location against its own baseline', async () => {
      const el = await render(withOutOfRange());

      const why = el.querySelector('.detail .why')?.textContent?.replace(/\s+/g, ' ').trim();
      expect(why).toContain('0 events this week.');
      expect(why).toContain('based on its own 8 previous weeks, May 25 – Jul 19. That is below its normal range.');
      expect(el.querySelectorAll('.detail app-week-history-chart rect.bar')).toHaveLength(9);
    });

    it('opens and closes a location detail on click', async () => {
      const fixture = TestBed.createComponent(Dashboard);
      http.expectOne('/api/accounts').flush(ACCOUNTS);
      await fixture.whenStable();
      http.expectOne(r => r.url.endsWith('/activity-baseline')).flush(baseline());
      await fixture.whenStable();
      const el = fixture.nativeElement as HTMLElement;
      expect(el.querySelector('.detail')).toBeNull();

      el.querySelectorAll<HTMLButtonElement>('button.row')[2].click();
      await fixture.whenStable();
      expect(el.querySelectorAll('.detail')).toHaveLength(1);

      el.querySelectorAll<HTMLButtonElement>('button.row')[2].click();
      await fixture.whenStable();
      expect(el.querySelector('.detail')).toBeNull();
    });
  });

  it('shows the empty state for an account with no activity', async () => {
    const data = baseline();
    data.account = ACCOUNTS[1];
    data.locations = [];
    data.metrics = data.metrics.map(m => ({ ...m, status: 'no_data' as const }));

    const el = await render(data);

    expect(el.querySelector('.empty')?.textContent).toContain('No activity recorded yet');
    expect(el.querySelector('h1')).toBeNull();
    expect(el.querySelectorAll('.mcard, button.row')).toHaveLength(0);
  });

  it('lists exceptional baseline weeks and excluded duplicates in the data notes', async () => {
    const el = await render();

    const notes = Array.from(el.querySelectorAll('.two .card:last-child li')).map(li => li.textContent?.replace(/\s+/g, ' ').trim());
    expect(notes).toEqual([
      '1 duplicated event was excluded from the weeks shown.',
      'The week of Jun 1 (880 events) was exceptional. It is in your history but does not move the normal range.',
      'The week of Jul 27 is still in progress and is not compared yet.'
    ]);
  });
});
