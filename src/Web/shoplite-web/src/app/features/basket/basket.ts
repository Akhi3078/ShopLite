import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { BasketService } from '../../core/basket.service';
import { BasketItem } from '../../core/models';

/**
 * The basket screen.
 *
 * Its job beyond the obvious: make the Phase 2 failure modes visible. A price served from
 * Basket's fallback cache is badged, and a Catalog outage produces a message that says so
 * rather than a generic "something went wrong".
 */
@Component({
  selector: 'app-basket',
  imports: [RouterLink, CurrencyPipe],
  templateUrl: './basket.html',
})
export class BasketPage {
  protected readonly basketService = inject(BasketService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  /** Which line is mid-request, so only that row's controls disable. */
  protected readonly busyProductId = signal<string | null>(null);

  protected readonly clearing = signal(false);
  protected readonly checkingOut = signal(false);

  private readonly router = inject(Router);

  constructor() {
    this.load();
  }

  /**
   * Checks out, then navigates to the orders screen.
   *
   * Note what it cannot do: navigate to the new order. Checkout returns 202 and no order
   * id, because the order does not exist yet — Ordering creates it when it receives the
   * event. The `checkedOut=1` flag tells the orders screen to explain the wait rather
   * than show an empty list as though something had gone wrong.
   */
  protected checkout(): void {
    this.checkingOut.set(true);
    this.error.set(null);

    this.basketService.checkout().subscribe({
      next: () => {
        this.checkingOut.set(false);
        void this.router.navigate(['/orders'], { queryParams: { checkedOut: '1' } });
      },
      error: (response: HttpErrorResponse) => {
        this.checkingOut.set(false);

        this.error.set(
          response.status === 409
            ? 'Your basket changed while checking out. Reload the page and try again.'
            : this.describe(response, 'Checkout failed. Your basket has not been changed.'),
        );
      },
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.basketService.refresh().subscribe({
      next: () => this.loading.set(false),
      error: (response: HttpErrorResponse) => {
        this.error.set(this.describe(response, 'Could not load your basket.'));
        this.loading.set(false);
      },
    });
  }

  protected increase(item: BasketItem): void {
    this.setQuantity(item, item.quantity + 1);
  }

  protected decrease(item: BasketItem): void {
    // 1 is the floor; going lower is a removal, which is a different intent and a
    // different button.
    if (item.quantity > 1) {
      this.setQuantity(item, item.quantity - 1);
    }
  }

  protected remove(item: BasketItem): void {
    this.busyProductId.set(item.productId);
    this.error.set(null);

    this.basketService.removeItem(item.productId).subscribe({
      next: () => this.busyProductId.set(null),
      error: (response: HttpErrorResponse) => {
        this.error.set(this.describe(response, `Could not remove "${item.productName}".`));
        this.busyProductId.set(null);
      },
    });
  }

  protected clear(): void {
    if (!confirm('Empty your basket?')) {
      return;
    }

    this.clearing.set(true);
    this.error.set(null);

    this.basketService.clear().subscribe({
      next: () => this.clearing.set(false),
      error: (response: HttpErrorResponse) => {
        this.error.set(this.describe(response, 'Could not empty your basket.'));
        this.clearing.set(false);
      },
    });
  }

  private setQuantity(item: BasketItem, quantity: number): void {
    this.busyProductId.set(item.productId);
    this.error.set(null);

    this.basketService.setQuantity(item.productId, quantity).subscribe({
      next: () => this.busyProductId.set(null),
      error: (response: HttpErrorResponse) => {
        this.error.set(this.describe(response, `Could not change "${item.productName}".`));
        this.busyProductId.set(null);
      },
    });
  }

  /**
   * Turns a failed response into something a human can act on.
   *
   * 503 is the interesting one: it means Basket is healthy but Catalog is not, and Basket
   * had no cached price to fall back on. Reporting that as a generic error would hide the
   * one thing worth knowing — that the problem is upstream and likely temporary.
   *
   * 401 and 403 never reach here; the interceptor redirects first.
   */
  private describe(response: HttpErrorResponse, fallback: string): string {
    if (response.status === 503) {
      return 'The Catalog service is unavailable and no cached price was available, so this product could not be priced. Try again shortly.';
    }

    if (response.status === 404) {
      return 'That product is no longer in the catalog.';
    }

    if (response.status === 0) {
      return 'Could not reach the API. Is the gateway running?';
    }

    return fallback;
  }
}
