import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-forbidden',
  imports: [RouterLink],
  template: `
    <div class="text-center py-5">
      <i class="bi bi-shield-lock text-warning" style="font-size: 3rem"></i>
      <h1 class="h3 mt-3">Not allowed</h1>
      <p class="text-secondary">
        Your account does not have the role this page requires.
      </p>
      <p class="text-secondary small">
        Signed in as <strong>{{ auth.email() }}</strong> with
        @if (auth.roles().length) {
          role {{ auth.roles().join(', ') }}.
        } @else {
          no roles.
        }
      </p>
      <a class="btn btn-primary mt-2" routerLink="/products">Back to products</a>
    </div>
  `,
})
export class Forbidden {
  protected readonly auth = inject(AuthService);
}
