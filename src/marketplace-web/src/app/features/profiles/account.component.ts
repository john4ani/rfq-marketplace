import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ProfileEditorComponent } from './profile-editor.component';
import { RepresentedCompaniesComponent } from './represented-companies.component';
import { TrustSummaryComponent } from './trust-summary.component';

@Component({
  selector: 'app-account',
  imports: [ProfileEditorComponent, RepresentedCompaniesComponent, TrustSummaryComponent],
  template: `<section class="account-shell">
    <header><div><p class="eyebrow">Contul meu</p><h1>Datele și relațiile tale în CompaSpot</h1>
      <p>Contul tău este personal. Poți cumpăra oricând și poți reprezenta una sau mai multe firme.</p></div></header>
    <app-profile-editor />
    <app-represented-companies />
    <app-trust-summary [refreshVersion]="trustVersion()" />
    <section class="profile-card security-card"><div class="section-heading"><div><p class="eyebrow">Securitate</p><h2>Accesul la cont</h2></div><p>Folosește verificarea e-mailului de mai sus, când este necesară.</p></div>
      <button type="button" class="secondary" (click)="signOut()">Deconectare</button></section>
  </section>`,
  styleUrl: './account.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AccountComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly trustVersion = signal(0);
  signOut(): void { this.auth.logout().subscribe({ next: () => void this.router.navigateByUrl('/sign-in') }); }
}
