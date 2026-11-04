import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Blocks a route unless someone is signed in, remembering where they were going. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login'], {
    queryParams: { returnUrl: state.url },
  });
};

/**
 * Blocks a route unless the user holds one of the given roles.
 *
 * A factory rather than a guard, so routes read `roleGuard(Role.Admin)`.
 *
 * Convenience, not security: this only stops navigation in this browser. The API
 * enforces the same rule server-side, and that is the check that counts.
 */
export const roleGuard =
  (...roles: string[]): CanActivateFn =>
  () => {
    const auth = inject(AuthService);
    const router = inject(Router);

    if (!auth.isAuthenticated()) {
      return router.createUrlTree(['/login']);
    }

    return auth.hasAnyRole(...roles) ? true : router.createUrlTree(['/forbidden']);
  };
