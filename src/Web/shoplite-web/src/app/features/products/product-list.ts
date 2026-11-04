import { CurrencyPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { CatalogService } from '../../core/catalog.service';
import { Product, Role } from '../../core/models';

@Component({
  selector: 'app-product-list',
  imports: [RouterLink, FormsModule, CurrencyPipe],
  templateUrl: './product-list.html',
})
export class ProductList {
  private readonly catalog = inject(CatalogService);

  protected readonly auth = inject(AuthService);

  protected readonly products = signal<Product[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly category = signal('');
  protected readonly deletingId = signal<string | null>(null);

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
