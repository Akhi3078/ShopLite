/** Role names. These strings must match ShopLite.ServiceDefaults.Roles on the server. */
export const Role = {
  Admin: 'Admin',
  Manager: 'Manager',
  Customer: 'Customer',
} as const;

export type RoleName = (typeof Role)[keyof typeof Role];

export interface AuthResponse {
  token: string;
  expiresAt: string;
  email: string;
  displayName: string;
  roles: string[];
}

export interface Product {
  id: string;
  name: string;
  price: number;
  category: string;
}

export interface ProductDetail extends Product {
  description: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface ProductInput {
  name: string;
  description: string;
  price: number;
  category: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface UserListItem {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
}

/**
 * A line in the basket.
 *
 * `unitPrice` is a snapshot taken when the item was added, not a live price — so a line
 * can legitimately disagree with the catalog, and that is intended: the customer is owed
 * the price they were shown.
 */
export interface BasketItem {
  productId: string;
  productName: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;

  /**
   * True when Catalog was unreachable and Basket priced this line from its own fallback
   * cache. Surfaced in the UI so a remembered price is never shown as if it were current.
   */
  priceFromCache: boolean;
}

export interface Basket {
  id: string;
  customerId: string;
  items: BasketItem[];
  total: number;

  /** True when any line was priced from the fallback cache. */
  containsCachedPrices: boolean;
}

/**
 * What checkout returns — deliberately not an order.
 *
 * The API answers 202 Accepted: the basket is emptied and the event is recorded, but
 * Ordering creates the order asynchronously, so there is no order id to hand back yet.
 * This is eventual consistency arriving at the UI, and the screen has to say so rather
 * than pretend the work is finished.
 */
export interface CheckoutResult {
  messageId: string;
  total: number;
  lineCount: number;
}

export interface OrderSummary {
  id: string;
  customerId: string;
  customerEmail: string;
  status: 'AwaitingFulfilment' | 'Completed' | 'Cancelled';
  total: number;
  lineCount: number;
  placedAt: string;
}

export interface OrderDetail {
  id: string;
  customerId: string;
  customerEmail: string;
  status: string;
  total: number;
  placedAt: string;
  updatedAt: string | null;
  lines: OrderDetailLine[];
}

export interface OrderDetailLine {
  productId: string;
  productName: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}
