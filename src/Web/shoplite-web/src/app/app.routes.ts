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
