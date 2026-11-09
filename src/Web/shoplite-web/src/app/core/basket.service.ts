import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { Basket, CheckoutResult } from './models';

/**
 * The browser's view of the Basket service.
 *
 * Holds the current basket in a signal so the navbar badge and the basket page read the
 * same state instead of each fetching it. Every call returns the whole basket, so there
 * is never a partial update to reconcile — the server is the only thing that decides what
 * a basket contains.
 */
@Injectable({ providedIn: 'root' })
export class BasketService {
  private readonly http = inject(HttpClient);

  /**
   * Note the path keeps its `/basket` prefix, unlike `/catalog/products` or
   * `/identity/auth`. The gateway strips those two prefixes because those services expose
   * their resources at the root; Basket's own resource is already `/basket`, so the
   * gateway forwards it unchanged. See ADR 0006.
   */
  private readonly base = `${environment.gatewayUrl}/basket`;

  private readonly state = signal<Basket | null>(null);

  readonly basket = this.state.asReadonly();

  /** Total units, not lines — what a cart badge conventionally shows. */
  readonly itemCount = computed(() =>
    (this.state()?.items ?? []).reduce((sum, item) => sum + item.quantity, 0),
  );

  readonly total = computed(() => this.state()?.total ?? 0);

  readonly hasCachedPrices = computed(() => this.state()?.containsCachedPrices ?? false);

  refresh() {
    return this.http.get<Basket>(this.base).pipe(tap((basket) => this.state.set(basket)));
  }

  /**
   * Adds a product, sending an `Idempotency-Key`.
   *
   * This header is the whole point of the Phase 2 idempotency work, from the client side.
   * Adding an item *increases* a quantity, so if this POST were retried — by a flaky
   * network, an impatient second click, or a gateway that timed out after the work had
   * already committed — the customer would end up with two of something they asked for
   * once. The key lets the server recognise the replay and do nothing.
   *
   * A fresh key per user action, not per HTTP attempt: retries of the *same* action must
   * carry the *same* key, or they are not recognisable as retries.
   */
  addItem(productId: string, quantity = 1) {
    const headers = new HttpHeaders({ 'Idempotency-Key': crypto.randomUUID() });

    return this.http
      .post<Basket>(`${this.base}/items`, { productId, quantity }, { headers })
      .pipe(tap((basket) => this.state.set(basket)));
  }

  /**
   * Sets a line to an absolute quantity.
   *
   * Deliberately no Idempotency-Key. This assigns rather than increments, so applying it
   * twice leaves the same result — it is safe to retry by its own design. Worth comparing
   * with addItem above: where you can choose the safe shape of an operation, you do not
   * need the machinery.
   */
  setQuantity(productId: string, quantity: number) {
    return this.http
      .put<Basket>(`${this.base}/items/${productId}`, { quantity })
      .pipe(tap((basket) => this.state.set(basket)));
  }

  removeItem(productId: string) {
    return this.http
      .delete<Basket>(`${this.base}/items/${productId}`)
      .pipe(tap((basket) => this.state.set(basket)));
  }

  clear() {
    return this.http.delete<void>(this.base).pipe(tap(() => this.state.set(null)));
  }

  /**
   * Checks out the basket. Returns 202, not an order.
   *
   * The local basket is cleared on success because the server has emptied it — but note
   * what cannot be done here: navigate to the new order. It does not exist yet. The
   * caller has to tell the customer that their order is on its way and let them look for
   * it, which is what asynchronous processing actually feels like from the outside.
   */
  checkout() {
    return this.http
      .post<CheckoutResult>(`${this.base}/checkout`, {})
      .pipe(tap(() => this.state.set(null)));
  }

  /**
   * Forgets the local copy without calling the server — used on sign-out, so the next
   * person to sign in on this browser does not briefly see the previous one's badge.
   * The basket itself survives on the server, keyed by customer.
   */
  reset(): void {
    this.state.set(null);
  }
}
