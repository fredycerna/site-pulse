import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { ActivityBaseline } from '../api/baseline-api';
import { Dashboard } from './dashboard';
import sample from '../api/testing/baseline-account-6.json';

const ACCOUNTS = [
  { id: 6, name: 'Metro Collision Centers', timezone: 'America/New_York' },
  { id: 20, name: 'Quiet Harbor Spa', timezone: 'America/Los_Angeles' }
];

describe('Dashboard URL state', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([{ path: '', component: Dashboard }])]
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function open(url: string) {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, Dashboard);
    http.expectOne('/api/accounts').flush(ACCOUNTS);
    await harness.fixture.whenStable();
    return harness;
  }

  function expectBaselineRequest(path: string) {
    const req = http.expectOne(r => r.url.endsWith('/activity-baseline'));
    expect(req.request.url).toBe(path);
    return req;
  }

  it('restores account, type and week from the URL', async () => {
    await open('/?account=20&type=call_received&week=2026-06-01');

    const req = expectBaselineRequest('/api/accounts/20/activity-baseline');
    expect(req.request.params.get('eventType')).toBe('call_received');
    expect(req.request.params.get('week')).toBe('2026-06-01');
    req.flush(sample);
  });

  it('falls back to defaults when the URL values are invalid', async () => {
    await open('/?account=abc&type=foo&week=last-tuesday');

    const req = expectBaselineRequest('/api/accounts/6/activity-baseline');
    expect(req.request.params.get('eventType')).toBe('all');
    expect(req.request.params.has('week')).toBe(false);
    req.flush(sample);
  });

  it('falls back to the first account when the account is not in the list', async () => {
    await open('/?account=999');

    expectBaselineRequest('/api/accounts/6/activity-baseline').flush(sample);
  });

  it('writes the selected metric card to the URL', async () => {
    const harness = await open('/?account=6');
    expectBaselineRequest('/api/accounts/6/activity-baseline').flush(sample as ActivityBaseline);
    await harness.fixture.whenStable();

    (harness.routeNativeElement?.querySelector('button[data-type="lead_created"]') as HTMLButtonElement).click();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/?account=6&type=lead_created');
    const req = expectBaselineRequest('/api/accounts/6/activity-baseline');
    expect(req.request.params.get('eventType')).toBe('lead_created');
    req.flush(sample);
  });
});
