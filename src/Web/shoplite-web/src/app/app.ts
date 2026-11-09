import { Component, effect, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';
import { BasketService } from './core/basket.service';
import { Role } from './core/models';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly router = inject(Router);

  protected readonly auth = inject(AuthService);
  protected readonly basket = inject(BasketService);

  constructor() {
    // The navbar shows a basket count, so the shell is what has to keep it current.
    //
    // Driving it from an effect on isAuthenticated covers both directions with one rule:
    // signing in (or arriving with a stored token) loads the basket, signing out forgets
    // it. Forgetting matters — without it the next person to sign in on this browser
    // would see the previous one's count until their first basket request returned.
    effect(() => {
      if (this.auth.isAuthenticated()) {
        // A failure here must stay silent. It would mean the Basket service is down,
        // which is worth reporting on the basket page, not as a broken navbar on every
        // other screen.
        this.basket.refresh().subscribe({ error: () => {} });
      } else {
        this.basket.reset();
      }
    });
  }

  protected isAdmin(): boolean {
    return this.auth.hasAnyRole(Role.Admin);
  }

  protected logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
