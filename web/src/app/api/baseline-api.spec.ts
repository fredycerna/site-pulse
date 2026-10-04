import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivityBaseline, BaselineApi } from './baseline-api';
import sample from './testing/baseline-account-6.json';

describe('BaselineApi', () => {
  let api: BaselineApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(BaselineApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requests the account baseline with the event type and week', () => {
    let result: ActivityBaseline | undefined;
    api.get({ accountId: 6, eventType: 'call_received', week: '2026-06-01' }).subscribe(r => (result = r));

    const req = http.expectOne(r => r.url === '/api/accounts/6/activity-baseline');
    expect(req.request.params.get('eventType')).toBe('call_received');
    expect(req.request.params.get('week')).toBe('2026-06-01');
    req.flush(sample);

    expect(result?.account.id).toBe(6);
  });

  it('omits the week so the API picks the last complete one', () => {
    api.get({ accountId: 6, eventType: 'all' }).subscribe();

    const req = http.expectOne(r => r.url === '/api/accounts/6/activity-baseline');
    expect(req.request.params.has('week')).toBe(false);
    req.flush(sample);
  });
});
