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
