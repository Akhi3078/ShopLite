import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../environments/environment';
import { OrderDetail, OrderSummary } from './models';

/**
 * The browser's view of the Ordering service.
 *
 * Read-only on purpose: there is no `create`. An order is produced by checking out a
 * basket, which publishes an event — not by asking Ordering to make one. Adding a create
 * method here would invite a second way to produce orders that skips checkout entirely.
 */
@Injectable({ providedIn: 'root' })
export class OrderService {
  private readonly http = inject(HttpClient);

  /** The gateway strips this prefix, so Ordering itself sees `/orders`. */
  private readonly base = `${environment.gatewayUrl}/ordering/orders`;

  list() {
    return this.http.get<OrderSummary[]>(this.base);
  }

  get(id: string) {
    return this.http.get<OrderDetail>(`${this.base}/${id}`);
  }
}
