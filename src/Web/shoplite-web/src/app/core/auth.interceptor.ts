import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * Attaches the bearer token to every outgoing request, and reacts to the two rejections
 * the server can give us.
 *
 * Doing this once here is why no component ever touches a header. The distinction the
 * two branches draw is worth internalising:
 *
 *   401 Unauthorized — "I don't know who you are."  The token is missing or expired, so
 *                      clear it and send them to sign in again.
 *   403 Forbidden    — "I know who you are, and no."  The token is fine; the role is
 *                      not. Signing in again would change nothing.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const token = auth.token;

  const authorized = token
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  return next(authorized).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        auth.logout();
        void router.navigate(['/login'], {
          queryParams: { returnUrl: router.url, expired: true },
        });
      }

      if (error.status === 403) {
        void router.navigate(['/forbidden']);
      }

      return throwError(() => error);
    }),
  );
};
