import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register.html',
})
export class Register {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    displayName: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  protected readonly errors = signal<string[]>([]);
  protected readonly busy = signal(false);

  protected submit(): void {
    if (this.form.invalid || this.busy()) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.errors.set([]);

    const { email, password, displayName } = this.form.getRawValue();

    this.auth.register(email, password, displayName).subscribe({
      next: () => void this.router.navigate(['/products']),
      error: (response: HttpErrorResponse) => {
        // ASP.NET Core Identity returns its own rules (password complexity, duplicate
        // email) as an RFC 7807 validation problem. Flatten it for display.
        const problems = response.error?.errors as Record<string, string[]> | undefined;

        this.errors.set(
          problems
            ? Object.values(problems).flat()
            : ['Registration failed. Please try again.'],
        );

        this.busy.set(false);
      },
    });
  }
}
