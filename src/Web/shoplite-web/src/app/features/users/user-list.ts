import { Component, inject, signal } from '@angular/core';
import { UserService } from '../../core/user.service';
import { AuthService } from '../../core/auth.service';
import { UserListItem } from '../../core/models';

@Component({
  selector: 'app-user-list',
  templateUrl: './user-list.html',
})
export class UserList {
  private readonly users = inject(UserService);
  private readonly auth = inject(AuthService);

  protected readonly rows = signal<UserListItem[]>([]);
  protected readonly allRoles = signal<string[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly savingId = signal<string | null>(null);

  constructor() {
    this.users.availableRoles().subscribe({
      next: (roles) => this.allRoles.set(roles),
      error: () => this.allRoles.set([]),
    });

    this.load();
  }

  protected load(): void {
    this.loading.set(true);

    this.users.list().subscribe({
      next: (list) => {
        this.rows.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load users. Is the Identity service running?');
        this.loading.set(false);
      },
    });
  }

  protected isSelf(user: UserListItem): boolean {
    return user.email === this.auth.email();
  }

  protected toggleRole(user: UserListItem, role: string): void {
    const next = user.roles.includes(role)
      ? user.roles.filter((r) => r !== role)
      : [...user.roles, role];

    this.savingId.set(user.id);
    this.error.set(null);

    this.users.setRoles(user.id, next).subscribe({
      next: () => {
        this.rows.update((rows) =>
          rows.map((row) => (row.id === user.id ? { ...row, roles: next } : row)),
        );
        this.savingId.set(null);
      },
      error: () => {
        this.error.set(`Could not update roles for ${user.email}.`);
        this.savingId.set(null);
      },
    });
  }
}
