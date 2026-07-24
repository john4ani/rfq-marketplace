import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth/auth.service';
@Component({ selector: 'app-root', imports: [RouterLink, RouterLinkActive, RouterOutlet], templateUrl: './app.html', styleUrl: './app.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class App {
  readonly auth = inject(AuthService);
  readonly hasSupplierProfile = computed(() =>
    this.auth.profiles().some(profile => profile.accountType === 'Supplier'));
}
