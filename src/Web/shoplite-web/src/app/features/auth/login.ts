import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login.html',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);

  /** Seeded accounts, so the UI can be tried without registering first. */
  protected readonly demoAccounts = [
    { label: 'Admin', email: 'admin@shoplite.local' },
    { label: 'Manager', email: 'manager@shoplite.local' },
    { label: 'Customer', email: 'customer@shoplite.local' },
  ];

  protected useDemo(email: string): void {
    this.form.setValue({ email, password: 'Passw0rd!' });
  }

  protected submit(): void {
    if (this.form.invalid || this.busy()) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    const { email, password } = this.form.getRawValue();

    this.auth.login(email, password).subscribe({
      next: () => {
        const returnUrl = new URLSearchParams(window.location.search).get('returnUrl');
        void this.router.navigateByUrl(returnUrl ?? '/products');
      },
      error: () => {
        // The API deliberately does not say which of the two was wrong.
        this.error.set('Email or password is incorrect.');
        this.busy.set(false);
      },
    });
  }
}
