import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface AccountSummary {
  id: number;
  name: string;
  timezone: string;
}

@Injectable({ providedIn: 'root' })
export class AccountsApi {
  private readonly http = inject(HttpClient);

  list(): Observable<AccountSummary[]> {
    return this.http.get<AccountSummary[]>('/api/accounts');
  }
}
