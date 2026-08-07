import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { AccountSwitcherComponent } from '../profiles/account-switcher.component';
import { ProfileEditorComponent } from '../profiles/profile-editor.component';
import { SupplierSettingsComponent } from '../profiles/supplier-settings.component';
import { BuyerRfqListComponent } from '../rfqs/buyer-rfq-list.component';
import { SupplierOfferListComponent } from '../offers/supplier-offer-list.component';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, AccountSwitcherComponent, ProfileEditorComponent, SupplierSettingsComponent, BuyerRfqListComponent, SupplierOfferListComponent],
  template: `<section class="dashboard-intro">
    <div><p class="eyebrow">Spațiul tău de lucru</p><h1>Bine ai venit</h1>
      <p>Gestionează cererile, ofertele și preferințele contului dintr-un singur loc.</p></div>
    <button class="secondary" type="button" (click)="signOut()" [disabled]="signingOut()">
      {{ signingOut() ? 'Deconectare…' : 'Deconectare' }}
    </button>
  </section>

  <section class="workspace-bar">
    <div><span>Profil activ</span><app-account-switcher (profileAdded)="openNewProfile($event)" /></div>
    <nav aria-label="Secțiuni cont">
      <button type="button" [class.active]="section()==='overview'" (click)="section.set('overview')">Activitate</button>
      <button type="button" [class.active]="section()==='settings'" (click)="section.set('settings')">Profil și preferințe</button>
    </nav>
  </section>

  <section class="dashboard-content">
    @if(section()==='overview'){
      <div class="quick-actions">
        @if(hasBuyerProfile()){
          <a class="action-card primary" routerLink="/rfqs/new"><span>+</span><div><strong>Creează o cerere</strong><small>Descrie nevoia și public-o în piață</small></div></a>
        }
        @if(auth.session()?.accountType==='Buyer'){
          <a class="action-card" routerLink="/"><span>PL</span><div><strong>Vezi Piața Live</strong><small>Urmărește activitatea comunității</small></div></a>
        }@else{
          <a class="action-card primary" routerLink="/open-requests"><span>↗</span><div><strong>Găsește oportunități</strong><small>Vezi cererile potrivite profilului tău</small></div></a>
          <button class="action-card" type="button" (click)="section.set('settings')"><span>✓</span><div><strong>Verifică preferințele</strong><small>Categoriile și zonele influențează potrivirile</small></div></button>
        }
      </div>
      <app-buyer-rfq-list />
      @if(auth.session()?.accountType==='Supplier'){
        <app-supplier-offer-list />
        <section class="activity-card"><p class="eyebrow">Furnizor</p><h2>Cererile potrivite sunt în Piața Live</h2>
          <p>Explorează nevoile deschise, verifică detaliile și trimite oferta direct din pagina cererii.</p>
          <a routerLink="/open-requests">Deschide cererile potrivite →</a></section>
      }
    }@else{
      <div class="settings-intro"><div><p class="eyebrow">Configurare</p><h2>Profil și preferințe</h2></div>
        <p>Informațiile complete îmbunătățesc relevanța potrivirilor și încrederea partenerilor.</p></div>
      @if (returnUrl()) {
        <section class="onboarding-callout" role="status">
          <strong>Completează profilul de cumpărător înainte de prima cerere.</strong>
          <span>Folosim localitatea și raza de interes pentru livrare și potrivirea furnizorilor. După salvare, revenim automat la cerere.</span>
        </section>
      }
      <app-profile-editor (profileSaved)="resumeAfterProfileSave($event)" />
      <app-supplier-settings />
    }
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
  </section>`,
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardComponent {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly signingOut = signal(false);
  readonly error = signal('');
  readonly section = signal<'overview' | 'settings'>(this.route.snapshot.queryParamMap.get('section') === 'settings' ? 'settings' : 'overview');
  readonly returnUrl = signal(this.safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl')));
  readonly hasBuyerProfile = computed(() => this.auth.profiles().some(profile => profile.accountType === 'Buyer'));

  openNewProfile(accountType: 'Buyer' | 'Supplier'): void {
    this.auth.switchProfile(accountType);
    this.returnUrl.set(null);
    this.section.set('settings');
  }

  resumeAfterProfileSave(accountType: 'Buyer' | 'Supplier'): void {
    const destination = this.returnUrl();
    if (destination && (accountType === 'Buyer' && destination === '/rfqs/new'
      || accountType === 'Supplier' && destination.startsWith('/supplier-invitations/')))
      void this.router.navigateByUrl(destination);
  }

  private safeReturnUrl(value: string | null): string | null {
    return value === '/rfqs/new' || value?.startsWith('/supplier-invitations/') ? value : null;
  }

  signOut(): void {
    this.signingOut.set(true);
    this.error.set('');
    this.auth.logout().subscribe({
      next: () => void this.router.navigateByUrl('/sign-in'),
      error: () => {
        this.signingOut.set(false);
        this.error.set('Sesiunea locală a fost închisă, dar API-ul nu a putut revoca sesiunea de pe server. Porniți API-ul și încearcă din nou.');
      }
    });
  }
}
