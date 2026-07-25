import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth/auth.service';
import { BackendAvailabilityService } from './core/connectivity/backend-availability.service';
import { NotificationMenuComponent } from './features/notifications/notification-menu.component';
@Component({ selector: 'app-root', imports: [RouterLink, RouterLinkActive, RouterOutlet, NotificationMenuComponent], templateUrl: './app.html', styleUrl: './app.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class App {
  readonly auth = inject(AuthService);
  readonly backend = inject(BackendAvailabilityService);
  readonly hasSupplierProfile = computed(() =>
    this.auth.profiles().some(profile => profile.accountType === 'Supplier'));
}
