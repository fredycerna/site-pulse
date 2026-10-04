import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
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
      providers: [provideHttpClient(), provideHttpClientTesting()]
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

  it('says how many weeks are missing when there is no baseline yet', async () => {
    const data = baseline();
    data.week = { start: '2026-02-16', end: '2026-02-23' };
    data.metrics[0].status = 'insufficient_history';

    const el = await render(data);

    expect(el.querySelector('h1')?.textContent?.trim()).toBe('No baseline yet');
    expect(el.querySelector('.sub')?.textContent).toContain('2 weeks to go.');
  });
});
