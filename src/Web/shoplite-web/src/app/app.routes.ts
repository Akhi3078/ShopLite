import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/guards';
import { Role } from './core/models';

/**
 * Routes are lazy-loaded with `loadComponent`, so a Customer never downloads the admin
 * screens at all.
 *
 * Note the guard layering: `authGuard` answers "are you signed in", `roleGuard` answers
 * "are you allowed". Keeping them separate means an unauthenticated user gets sent to
 * sign in, while a signed-in Customer gets told no — two different outcomes that a
 * single combined guard would blur.
 */
export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login').then((m) => m.Login),
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register').then((m) => m.Register),
  },
  {
    path: 'products',
    canActivate: [authGuard],
    loadComponent: () => import('./features/products/product-list').then((m) => m.ProductList),
  },
  {
    path: 'products/new',
    canActivate: [authGuard, roleGuard(Role.Admin, Role.Manager)],
    loadComponent: () => import('./features/products/product-form').then((m) => m.ProductForm),
  },
  {
    path: 'products/:id/edit',
    canActivate: [authGuard, roleGuard(Role.Admin, Role.Manager)],
    loadComponent: () => import('./features/products/product-form').then((m) => m.ProductForm),
  },
  {
    // Any signed-in user has a basket — there is no role gate, because the basket is
    // always the caller's own. The server works this out from the token, so the URL
    // carries no customer id and there is nothing here to authorise beyond "signed in".
    path: 'basket',
    canActivate: [authGuard],
    loadComponent: () => import('./features/basket/basket').then((m) => m.BasketPage),
  },
  {
    // Any signed-in user sees their own orders; an Admin sees everyone's. The difference
    // is decided by the API from the token, so there is no role gate on the route.
    path: 'orders',
    canActivate: [authGuard],
    loadComponent: () => import('./features/orders/order-list').then((m) => m.OrderList),
  },
  {
    path: 'users',
    canActivate: [authGuard, roleGuard(Role.Admin)],
    loadComponent: () => import('./features/users/user-list').then((m) => m.UserList),
  },
  {
    path: 'forbidden',
    loadComponent: () => import('./features/forbidden').then((m) => m.Forbidden),
  },
  { path: '', redirectTo: 'products', pathMatch: 'full' },
  { path: '**', redirectTo: 'products' },
];
