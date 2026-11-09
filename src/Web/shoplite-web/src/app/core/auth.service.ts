import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthResponse } from './models';

const STORAGE_KEY = 'shoplite.auth';

interface StoredAuth {
  token: string;
  expiresAt: string;
  email: string;
  displayName: string;
  roles: string[];
}

function readStorage(): StoredAuth | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;

    const parsed = JSON.parse(raw) as StoredAuth;

    // A stored token past its expiry is worse than none: it produces 401s that look
    // like bugs. Discard it on load.
    return new Date(parsed.expiresAt) > new Date() ? parsed : null;
  } catch {
    return null;
  }
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  /** The single source of truth for who is signed in. */
  private readonly auth = signal<StoredAuth | null>(readStorage());

  readonly isAuthenticated = computed(() => this.auth() !== null);
  readonly displayName = computed(() => this.auth()?.displayName ?? '');
  readonly email = computed(() => this.auth()?.email ?? '');
  readonly roles = computed(() => this.auth()?.roles ?? []);

  get token(): string | null {
    return this.auth()?.token ?? null;
  }

  login(email: string, password: string) {
    return this.http
      .post<AuthResponse>(`${environment.gatewayUrl}/identity/auth/login`, { email, password })
      .pipe(tap((response) => this.store(response)));
  }

  register(email: string, password: string, displayName: string) {
    return this.http
      .post<AuthResponse>(`${environment.gatewayUrl}/identity/auth/register`, {
        email,
        password,
        displayName,
      })
      .pipe(tap((response) => this.store(response)));
  }

  logout(): void {
    localStorage.removeItem(STORAGE_KEY);
    this.auth.set(null);
  }

  /**
   * Whether the signed-in user holds any of the given roles.
   *
   * This drives what the UI shows, and it is NOT security. The roles come from a token
   * this browser holds, and anything in the browser can be edited. The real check is
   * `RequireAuthorization` on the server; hiding a button only stops honest mistakes.
   */
  hasAnyRole(...roles: string[]): boolean {
    const mine = this.roles();
    return roles.some((role) => mine.includes(role));
  }

  private store(response: AuthResponse): void {
    const stored: StoredAuth = {
      token: response.token,
      expiresAt: response.expiresAt,
      email: response.email,
      displayName: response.displayName,
      roles: response.roles,
    };

    localStorage.setItem(STORAGE_KEY, JSON.stringify(stored));
    this.auth.set(stored);
  }
}
