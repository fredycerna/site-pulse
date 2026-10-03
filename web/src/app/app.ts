import { Component, inject, signal } from '@angular/core';
import { AccountSummary, AccountsApi } from './api/accounts-api';

@Component({
  selector: 'app-root',
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  private readonly accountsApi = inject(AccountsApi);

  protected readonly accounts = signal<AccountSummary[]>([]);
  protected readonly status = signal<'loading' | 'ready' | 'error'>('loading');

  constructor() {
    this.accountsApi.list().subscribe({
      next: accounts => {
        this.accounts.set(accounts);
        this.status.set('ready');
      },
      error: () => this.status.set('error')
    });
  }
}
