import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { OrderService } from '../../core/order.service';
import { OrderSummary, Role } from '../../core/models';

/**
 * The orders screen.
 *
 * Its particular job in this phase is to make eventual consistency visible. Checkout
 * returns before the order exists, so the obvious implementation — load once and show
 * what came back — would show an empty list to a customer who has just checked out, and
 * look like a bug. Instead this offers an explicit refresh and explains the gap.
 */
@Component({
  selector: 'app-order-list',
  imports: [RouterLink, CurrencyPipe, DatePipe],
  templateUrl: './order-list.html',
})
export class OrderList implements OnInit {
  private readonly orders = inject(OrderService);

  protected readonly auth = inject(AuthService);

  protected readonly items = signal<OrderSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  /** Set when arriving straight from checkout, so the wait can be explained. */
  protected readonly justCheckedOut = signal(false);

  ngOnInit(): void {
    // The basket page navigates here with this flag after a successful checkout.
    this.justCheckedOut.set(
      new URLSearchParams(window.location.search).get('checkedOut') === '1',
    );

    this.load();
  }

  protected isAdmin(): boolean {
    return this.auth.hasAnyRole(Role.Admin);
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.orders.list().subscribe({
      next: (orders) => {
        this.items.set(orders);
        this.loading.set(false);
      },
      error: (response: HttpErrorResponse) => {
        this.error.set(
          response.status === 0
            ? 'Could not reach the API. Is the gateway running?'
            : 'Could not load your orders.',
        );
        this.loading.set(false);
      },
    });
  }

  protected badgeClass(status: string): string {
    switch (status) {
      case 'Completed':
        return 'text-bg-success';
      case 'Cancelled':
        return 'text-bg-danger';
      default:
        // AwaitingFulfilment. Not a warning — it is the normal state in Phase 3, because
        // nothing yet exists to move an order out of it.
        return 'text-bg-secondary';
    }
  }
}
