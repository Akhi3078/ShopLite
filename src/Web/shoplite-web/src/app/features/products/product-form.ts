import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { CatalogService } from '../../core/catalog.service';

@Component({
  selector: 'app-product-form',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './product-form.html',
})
export class ProductForm implements OnInit {
  private readonly catalog = inject(CatalogService);
  private readonly router = inject(Router);

  /** Bound from the :id route parameter by withComponentInputBinding(). */
  readonly id = input<string>();

  protected readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.maxLength(2000)]],
    price: [0, [Validators.required, Validators.min(0.01)]],
    category: ['', [Validators.required, Validators.maxLength(100)]],
  });

  protected readonly errors = signal<string[]>([]);
  protected readonly busy = signal(false);
  protected readonly loading = signal(false);

  /**
   * Loads the product being edited, if there is one.
   *
   * This must not happen in the constructor. `withComponentInputBinding()` has the router
   * set `id` *after* the component is constructed, so a constructor reading `this.id()`
   * sees `undefined`, skips the load, and leaves the form blank — which is exactly the
   * bug this replaced. `ngOnInit` runs after inputs are bound, so the id is there.
   *
   * If this route ever became reachable from another edit URL (Angular reuses the
   * component instance and only changes the input, so `ngOnInit` would not run again),
   * this would need to be an `effect` on `this.id` instead. Today you can only arrive
   * here from the list, so the instance is always fresh.
   */
  ngOnInit(): void {
    const id = this.id();

    if (!id) {
      // Create mode: nothing to load, the empty form is correct.
      return;
    }

    this.loading.set(true);

    this.catalog.get(id).subscribe({
      next: (product) => {
        this.form.setValue({
          name: product.name,
          description: product.description,
          price: product.price,
          category: product.category,
        });
        this.loading.set(false);
      },
      error: (response: HttpErrorResponse) => {
        this.errors.set([
          response.status === 404
            ? 'That product no longer exists.'
            : 'Could not load this product.',
        ]);
        this.loading.set(false);
      },
    });
  }

  protected submit(): void {
    if (this.form.invalid || this.busy()) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.errors.set([]);

    const value = this.form.getRawValue();
    const id = this.id();

    // Create returns the new id, update returns nothing. Neither is used here, so the
    // two are widened to a common type rather than branching the subscribe twice.
    const request: Observable<unknown> = id
      ? this.catalog.update(id, value)
      : this.catalog.create(value);

    request.subscribe({
      next: () => void this.router.navigate(['/products']),
      error: (response: HttpErrorResponse) => {
        // The server validates independently of this form. Showing its messages rather
        // than a generic failure is what makes a 400 useful instead of mysterious.
        const problems = response.error?.errors as Record<string, string[]> | undefined;

        this.errors.set(
          problems ? Object.values(problems).flat() : ['Could not save the product.'],
        );

        this.busy.set(false);
      },
    });
  }
}
