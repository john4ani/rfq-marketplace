import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from './core/auth/auth.service';
import { BackendAvailabilityService } from './core/connectivity/backend-availability.service';
import { NotificationMenuComponent } from './features/notifications/notification-menu.component';
import { MessagesMenuComponent } from './features/messaging/messages-menu.component';

type NavContext = 'buyer' | 'company' | 'admin';

@Component({ selector: 'app-root', imports: [RouterLink, RouterLinkActive, RouterOutlet, NotificationMenuComponent, MessagesMenuComponent], templateUrl: './app.html', styleUrl: './app.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class App {
  readonly auth = inject(AuthService);
  readonly backend = inject(BackendAvailabilityService);
  private readonly router = inject(Router);
  readonly signingOut = signal(false);
  readonly mobileNavigationOpen = signal(false);
  private readonly currentUrl = signal(this.router.url);

  // Navigation is contextual, not a permanent mode: selling routes show the company rail, /admin shows the admin rail,
  // everything else shows the user-first buyer rail. The brand logo and "Înapoi în CompaSpot" always return home.
  readonly navContext = computed<NavContext>(() => {
    const url = this.currentUrl();
    if (url.startsWith('/admin')) return 'admin';
    if (url.startsWith('/opportunities') || url.startsWith('/company/offers')) return 'company';
    return 'buyer';
  });
  readonly companyContextId = computed(() => {
    const match = /[?&]companyId=([^&]+)/.exec(this.currentUrl());
    return match ? decodeURIComponent(match[1]) : '';
  });
  readonly companyContextParams = computed(() => this.companyContextId() ? { companyId: this.companyContextId() } : null);
  readonly companyLink = computed(() => this.companyContextId() ? ['/account/company', this.companyContextId()] : ['/account']);

  constructor() {
    this.router.events.pipe(filter(event => event instanceof NavigationEnd), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(event => this.currentUrl.set((event as NavigationEnd).urlAfterRedirects));
  }

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
