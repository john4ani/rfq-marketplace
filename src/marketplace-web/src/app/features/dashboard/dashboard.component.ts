import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { AccountSwitcherComponent } from '../profiles/account-switcher.component';
import { ProfileEditorComponent } from '../profiles/profile-editor.component';
import { SupplierSettingsComponent } from '../profiles/supplier-settings.component';
import { BuyerRfqListComponent } from '../rfqs/buyer-rfq-list.component';
import { SupplierOfferListComponent } from '../offers/supplier-offer-list.component';
import { TrustSummaryComponent } from '../profiles/trust-summary.component';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, FormsModule, AccountSwitcherComponent, ProfileEditorComponent, SupplierSettingsComponent, BuyerRfqListComponent, SupplierOfferListComponent, TrustSummaryComponent],
  template: `@if(section()==='overview'){
    @if(isBuyer()) {
      <section class="need-starter" aria-labelledby="need-starter-title">
        <div><h1 id="need-starter-title">Bună! Cu ce te putem ajuta?</h1>
          <p>Scrie-ne ce ai nevoie, așa cum i-ai spune unui furnizor.</p></div>
        <div class="need-entry"><label class="sr-only" for="dashboard-need">Descrie pe scurt ce ai nevoie</label><input id="dashboard-need" [(ngModel)]="needText" (keyup.enter)="startNeed()" placeholder="Am nevoie de cherestea pentru un acoperiș de 10 × 8 m"><button type="button" (click)="startNeed()">Continuă</button></div>
        <div class="starter-reassurance"><span>AI-ul te ajută să clarifici</span><span>Nimic nu este trimis fără confirmarea ta</span></div>
        <a class="manual-entry" routerLink="/rfqs/new/advanced">Am deja lista / completez manual</a>
      </section>
    } @else {
      <section class="supplier-intro">
        <div><p class="eyebrow">Spațiul furnizorului</p><h1>Transformă cererile potrivite în colaborări.</h1>
          <p>Vezi oportunitățile relevante, răspunde simplu și urmărește fiecare ofertă dintr-un singur loc.</p></div>
        <a routerLink="/open-requests">Explorează cererile <span aria-hidden="true">→</span></a>
      </section>
    }
  }

  @if (!isBuyer() || section() !== 'overview') { <section class="workspace-bar">
    <div><span>Lucrezi ca</span><app-account-switcher (profileAdded)="openNewProfile($event)" /></div>
    <nav aria-label="Secțiuni cont"><button type="button" [class.active]="section()==='overview'" (click)="showOverview()">Activitate</button><button type="button" [class.active]="section()==='settings'" (click)="showSettings()">Profil și preferințe</button></nav>
  </section> }

  <section class="dashboard-content">
    @if(section()==='overview'){
      @if(!isBuyer()){<div class="quick-actions">
          <a class="action-card primary" routerLink="/open-requests"><span>↗</span><div><strong>Vezi cererile potrivite</strong><small>Răspunde rapid oportunităților relevante.</small></div><b aria-hidden="true">›</b></a>
          <button class="action-card" type="button" (click)="showSettings()"><span>✓</span><div><strong>Verifică preferințele</strong><small>Categoriile și zonele influențează potrivirile.</small></div><b aria-hidden="true">›</b></button>
      </div>}
      @if(auth.session()?.accountType==='Supplier'){
        <div id="supplier-offers"><app-supplier-offer-list /></div>
        <section class="activity-card"><div><p class="eyebrow">Următorul pas</p><h2>Ține profilul relevant pentru potriviri mai bune</h2>
          <p>Categoriile și zonele de livrare determină cererile pe care le vezi. Semnalele verificate ajută cumpărătorii să decidă cu încredere.</p></div>
          <button type="button" class="secondary-action" (click)="showSettings()">Verifică profilul și preferințele</button></section>
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
      <app-trust-summary [refreshVersion]="trustVersion()" />
    }
  </section>`,
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardComponent {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly section = signal<'overview' | 'settings'>(
    this.route.snapshot.routeConfig?.path === 'account' || this.route.snapshot.queryParamMap.get('section') === 'settings'
      ? 'settings'
      : 'overview');
  readonly returnUrl = signal(this.safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl')));
  readonly hasBuyerProfile = computed(() => this.auth.profiles().some(profile => profile.accountType === 'Buyer'));
  readonly isBuyer = computed(() => this.auth.session()?.accountType === 'Buyer');
  readonly trustVersion = signal(0);
  needText = '';

  startNeed(): void {
    const description = this.needText.trim();
    void this.router.navigate(['/rfqs/new'], { queryParams: description ? { description } : undefined });
  }

  showOverview(): void {
    this.section.set('overview');
    void this.router.navigateByUrl('/dashboard');
  }

  showSettings(): void {
    this.section.set('settings');
    void this.router.navigateByUrl('/account');
  }

  openNewProfile(accountType: 'Buyer' | 'Supplier'): void {
    this.auth.switchProfile(accountType);
    this.returnUrl.set(null);
    this.showSettings();
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
