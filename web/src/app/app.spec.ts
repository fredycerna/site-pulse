import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { App } from './app';

describe('App', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists the accounts returned by the API in the account selector', async () => {
    const fixture = TestBed.createComponent(App);
    http.expectOne('/api/accounts').flush([
      { id: 1, name: 'Summit Auto Group', timezone: 'America/Chicago' },
      { id: 6, name: 'Metro Collision Centers', timezone: 'America/New_York' }
    ]);
    await fixture.whenStable();

    const options = (fixture.nativeElement as HTMLElement).querySelectorAll('select#account option');
    expect(Array.from(options).map(o => o.textContent?.trim())).toEqual([
      'Summit Auto Group',
      'Metro Collision Centers'
    ]);
  });

  it('shows an error when the API call fails', async () => {
    const fixture = TestBed.createComponent(App);
    http.expectOne('/api/accounts').flush('down', { status: 503, statusText: 'Service Unavailable' });
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')).toBeTruthy();
  });
});
