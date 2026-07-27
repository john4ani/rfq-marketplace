import { ChangeDetectionStrategy, Component, computed, inject, output, signal } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-account-switcher',
  template: `<div>
  <nav class="profile-tabs" aria-label="Profil activ">
    @for (profile of auth.profiles(); track profile.accountType) {
      <button type="button" [class.active]="auth.session()?.accountType === profile.accountType"
        [attr.aria-pressed]="auth.session()?.accountType === profile.accountType"
        (click)="auth.switchProfile(profile.accountType)">
        {{ profile.accountType === 'Buyer' ? 'Cumpărător' : 'Furnizor' }}
      </button>
    }
    @if (!hasBuyerProfile()) {
      <button class="add-profile" type="button" [disabled]="addingProfile()"
        (click)="addProfile('Buyer')">+ Cumpărător</button>
    }
    @if (!hasSupplierProfile()) {
      <button class="add-profile" type="button" [disabled]="addingProfile()"
        (click)="addProfile('Supplier')">+ Furnizor</button>
    }
  </nav>
  @if (error()) { <p class="profile-switch-error" role="alert">{{ error() }}</p> }
  </div>`,
  styles: [`.profile-switch-error{margin:.4rem 0 0;color:#b42318;font-size:.86rem;font-weight:650}`],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AccountSwitcherComponent {
  readonly auth = inject(AuthService);
  readonly profileAdded = output<'Buyer' | 'Supplier'>();
  readonly addingProfile = signal(false);
  readonly error = signal('');
  readonly hasBuyerProfile = computed(() => this.auth.profiles().some(profile => profile.accountType === 'Buyer'));
  readonly hasSupplierProfile = computed(() => this.auth.profiles().some(profile => profile.accountType === 'Supplier'));

  addProfile(accountType: 'Buyer' | 'Supplier'): void {
    this.addingProfile.set(true);
    this.error.set('');
    this.auth.addProfile(accountType).subscribe({
      next: () => {
        this.addingProfile.set(false);
        this.profileAdded.emit(accountType);
      },
      error: () => {
        this.addingProfile.set(false);
        this.error.set('Profilul nu a putut fi adăugat. Verifică conexiunea și încearcă din nou.');
      }
    });
  }
}
