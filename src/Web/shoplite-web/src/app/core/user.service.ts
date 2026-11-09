import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../environments/environment';
import { UserListItem } from './models';

@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.gatewayUrl}/identity/users`;

  list() {
    return this.http.get<UserListItem[]>(this.base);
  }

  availableRoles() {
    return this.http.get<string[]>(`${this.base}/roles`);
  }

  setRoles(id: string, roles: string[]) {
    return this.http.put<void>(`${this.base}/${id}/roles`, { roles });
  }
}
