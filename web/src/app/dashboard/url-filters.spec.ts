import { convertToParamMap } from '@angular/router';
import { parseFilters } from './url-filters';

describe('parseFilters', () => {
  it('reads valid values', () => {
    expect(parseFilters(convertToParamMap({ account: '18', type: 'appointment_set', week: '2026-03-02' }))).toEqual({
      accountId: 18,
      eventType: 'appointment_set',
      week: '2026-03-02'
    });
  });

  it('defaults everything when the URL is empty', () => {
    expect(parseFilters(convertToParamMap({}))).toEqual({ accountId: null, eventType: 'all', week: undefined });
  });

  it.each([
    ['account', '-1'],
    ['account', '6abc'],
    ['account', '1e3'],
    ['account', ''],
    ['type', 'ALL'],
    ['type', 'toString'],
    ['week', '2026-6-1'],
    ['week', '2026-06-01T00:00']
  ])('ignores %s=%s', (key, value) => {
    expect(parseFilters(convertToParamMap({ [key]: value }))).toEqual({ accountId: null, eventType: 'all', week: undefined });
  });
});
