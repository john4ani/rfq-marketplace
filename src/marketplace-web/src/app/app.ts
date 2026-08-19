import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth/auth.service';
import { BackendAvailabilityService } from './core/connectivity/backend-availability.service';
import { NotificationMenuComponent } from './features/notifications/notification-menu.component';
@Component({ selector: 'app-root', imports: [RouterLink, RouterLinkActive, RouterOutlet, NotificationMenuComponent], templateUrl: './app.html', styleUrl: './app.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class App {
  readonly auth = inject(AuthService);
  readonly backend = inject(BackendAvailabilityService);
  private readonly router = inject(Router);
  readonly signingOut = signal(false);
  readonly mobileNavigationOpen = signal(false);

  isAuthRoute(): boolean {
    return ['/sign-in', '/register', '/forgot-password', '/reset-password', '/verify-email']
      .some(route => this.router.url === route || this.router.url.startsWith(`${route}?`));
  }

  toggleMobileNavigation(): void {
    this.mobileNavigationOpen.update(open => !open);
  }

  closeMobileNavigation(): void {
    this.mobileNavigationOpen.set(false);
  }

  signOut(): void {
    this.signingOut.set(true);
    this.auth.logout().subscribe({
      next: () => void this.router.navigateByUrl('/sign-in'),
      error: () => void this.router.navigateByUrl('/sign-in')
    });
  }
}
