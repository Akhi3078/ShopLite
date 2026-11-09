import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { BasketService } from '../../core/basket.service';
import { CatalogService } from '../../core/catalog.service';
import { Product, Role } from '../../core/models';

@Component({
  selector: 'app-product-list',
  imports: [RouterLink, FormsModule, CurrencyPipe],
  templateUrl: './product-list.html',
})
export class ProductList {
  private readonly catalog = inject(CatalogService);
  private readonly basket = inject(BasketService);

  protected readonly auth = inject(AuthService);

  protected readonly products = signal<Product[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly category = signal('');
  protected readonly deletingId = signal<string | null>(null);
  protected readonly addingId = signal<string | null>(null);

  /** Which product was last added, to confirm it briefly without a toast library. */
  protected readonly addedId = signal<string | null>(null);

  constructor() {
    this.load();
  }

  protected canManage(): boolean {
    return this.auth.hasAnyRole(Role.Admin, Role.Manager);
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.catalog.list(this.category() || undefined, 1, 50).subscribe({
      next: (page) => {
        this.products.set(page.items);
        this.totalCount.set(page.totalCount);
        this.loading.set(false);
      },
      error: () => {
        // 401 and 403 are already handled by the interceptor, which redirects.
        // Anything reaching here is a genuine failure worth showing.
        this.error.set('Could not load products. Is the Catalog service running?');
        this.loading.set(false);
      },
    });
  }

  /**
   * Adds one of this product to the basket.
   *
   * Worth knowing what this one click sets off on the server: Basket calls Catalog over
   * HTTP for the price, wrapped in a timeout, two retries and a circuit breaker, and
   * falls back to a cached price if Catalog cannot answer. If even that is unavailable
   * the request fails with 503 — which is the message handled below, and the reason this
   * button can fail in a way the Delete button next to it cannot.
   */
  protected addToBasket(product: Product): void {
    this.addingId.set(product.id);
    this.error.set(null);

    this.basket.addItem(product.id, 1).subscribe({
      next: () => {
        this.addingId.set(null);
        this.addedId.set(product.id);

        // Clear the confirmation after a moment. Deliberately not a timer stored on the
        // component: if the user navigates away, the stale set lands on a destroyed
        // component and Angular does not care, because signals are not change detection.
        setTimeout(() => {
          if (this.addedId() === product.id) {
            this.addedId.set(null);
          }
        }, 2000);
      },
      error: (response: HttpErrorResponse) => {
        this.addingId.set(null);

        this.error.set(
          response.status === 503
            ? `"${product.name}" could not be priced: the Catalog service is unavailable and Basket had no cached price for it.`
            : `Could not add "${product.name}" to your basket.`,
        );
      },
    });
  }

  protected remove(product: Product): void {
    if (!confirm(`Delete "${product.name}"?`)) {
      return;
    }

    this.deletingId.set(product.id);

    this.catalog.remove(product.id).subscribe({
      next: () => {
        this.products.update((list) => list.filter((p) => p.id !== product.id));
        this.totalCount.update((count) => count - 1);
        this.deletingId.set(null);
      },
      error: () => {
        this.error.set(`Could not delete "${product.name}".`);
        this.deletingId.set(null);
      },
    });
  }
}
