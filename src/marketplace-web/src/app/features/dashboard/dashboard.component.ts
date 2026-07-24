import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { AccountSwitcherComponent } from '../profiles/account-switcher.component';
import { ProfileEditorComponent } from '../profiles/profile-editor.component';
import { SupplierSettingsComponent } from '../profiles/supplier-settings.component';

@Component({
  selector: 'app-dashboard',
  imports: [AccountSwitcherComponent, ProfileEditorComponent, SupplierSettingsComponent],
  template: `<section class="dashboard-intro">
    <div><p class="eyebrow">Contul meu</p>
    <h1>Bine ai venit</h1>
    <app-account-switcher /></div>
    <button class="secondary" type="button" (click)="signOut()" [disabled]="signingOut()">
      {{ signingOut() ? 'Deconectare…' : 'Deconectare' }}
    </button>
  </section>
  <section class="dashboard-content">
    <app-profile-editor />
    <app-supplier-settings />
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
  </section>`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardComponent {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly signingOut = signal(false);
  readonly error = signal('');

  signOut(): void {
    this.signingOut.set(true);
    this.error.set('');
    this.auth.logout().subscribe({
      next: () => void this.router.navigateByUrl('/sign-in'),
      error: () => {
        this.signingOut.set(false);
        this.error.set('Sesiunea locală a fost închisă, dar API-ul nu a putut revoca sesiunea de pe server. Porniți API-ul și încercați din nou.');
      }
    });
  }
}
