import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-account-switcher',
  template: `<nav class="profile-tabs" aria-label="Profil activ">
    @for (profile of auth.profiles(); track profile.userId) {
      <button type="button" [class.active]="auth.session()?.accountType === profile.accountType"
        [attr.aria-pressed]="auth.session()?.accountType === profile.accountType"
        (click)="auth.switchProfile(profile.accountType)">
        {{ profile.accountType === 'Buyer' ? 'Cumpărător' : 'Furnizor' }}
      </button>
    }
  </nav>`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AccountSwitcherComponent {
  readonly auth = inject(AuthService);
}
