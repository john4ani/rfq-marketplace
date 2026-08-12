import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { AccountSwitcherComponent } from './account-switcher.component';
import { ProfileEditorComponent } from './profile-editor.component';
import { SupplierSettingsComponent } from './supplier-settings.component';
import { TrustSummaryComponent } from './trust-summary.component';

@Component({
  selector: 'app-account',
  imports: [AccountSwitcherComponent, ProfileEditorComponent, SupplierSettingsComponent, TrustSummaryComponent],
  template: `<section class="account-shell">
    <header><div><p class="eyebrow">Cont</p><h1>Profil și preferințe</h1>
      <p>Gestionează profilurile, zonele și categoriile care îți îmbunătățesc potrivirile.</p></div>
      <div class="profile-switch"><span>Lucrezi ca</span><app-account-switcher (profileAdded)="openNewProfile($event)" /></div>
    </header>
    @if (returnUrl()) {
      <section class="onboarding-callout" role="status"><strong>Completează profilul de cumpărător înainte de prima cerere.</strong>
        <span>Folosim localitatea și raza de interes pentru livrare și potrivirea furnizorilor. După salvare, revenim automat la cerere.</span></section>
    }
    <app-profile-editor (profileSaved)="resumeAfterProfileSave($event)" />
    <app-supplier-settings />
    <app-trust-summary [refreshVersion]="trustVersion()" />
  </section>`,
  styleUrl: './account.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AccountComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly returnUrl = signal(this.safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl')));
  readonly trustVersion = signal(0);

  openNewProfile(accountType: 'Buyer' | 'Supplier'): void {
    this.auth.switchProfile(accountType);
    this.returnUrl.set(null);
  }

  resumeAfterProfileSave(accountType: 'Buyer' | 'Supplier'): void {
    this.trustVersion.update(value => value + 1);
    const destination = this.returnUrl();
    if (destination && (accountType === 'Buyer' && destination === '/rfqs/new'
      || accountType === 'Supplier' && destination.startsWith('/supplier-invitations/')))
      void this.router.navigateByUrl(destination);
  }

  private safeReturnUrl(value: string | null): string | null {
    return value === '/rfqs/new' || value?.startsWith('/supplier-invitations/') ? value : null;
  }
}
