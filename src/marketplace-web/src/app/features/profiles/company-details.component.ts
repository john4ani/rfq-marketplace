import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { CompanyRegistryService } from '../../core/companies/company-registry.service';
import { ProfileService, SupplierProfile } from '../../core/profiles/profile.service';
import { SupplierSettingsComponent } from './supplier-settings.component';
import { CompanyVaultComponent } from './company-vault.component';

@Component({
  selector: 'app-company-details',
  imports: [RouterLink, SupplierSettingsComponent, CompanyVaultComponent],
  template: `<section class="account-shell"><a class="back" routerLink="/account">← Înapoi la cont</a>
    @if (loading()) { <p role="status">Se încarcă firma…</p> }
    @else if (company()) { <header><p class="eyebrow">Firmă reprezentată</p><h1>{{ company()!.tradingName }}</h1><p>{{ company()!.description || 'Detaliile acestei firme sunt disponibile pentru contextul tău de reprezentare.' }}</p></header>
      <section class="profile-card"><h2>Date firmă</h2><dl class="company-details"><div><dt>Contact</dt><dd>{{ company()!.contactPerson }}</dd></div><div><dt>E-mail</dt><dd>{{ company()!.contactEmail }}</dd></div><div><dt>Telefon</dt><dd>{{ company()!.phoneNumber }}</dd></div><div><dt>Locație</dt><dd>{{ company()!.city }}, {{ company()!.county }}</dd></div>@if(company()!.website){<div><dt>Website</dt><dd><a [href]="company()!.website" target="_blank" rel="noopener noreferrer">{{ company()!.website }}</a></dd></div>}</dl></section>
      <div class="company-actions"><a [routerLink]="'/opportunities'" [queryParams]="{ companyId: company()!.id }">Oportunități pentru firma mea</a><a [routerLink]="'/company/offers'" [queryParams]="{ companyId: company()!.id }">Ofertele firmei</a></div>
      @if (isAdmin()) {
        <section class="profile-card">
          <h2>Oferte gratuite · Auto-Bid</h2>
          @if (freeGenerations() !== null) {
            <p>Generări gratuite rămase pentru contul proprietarului firmei: <strong>{{ freeGenerations() }}</strong></p>
            <div class="company-actions"><button type="button" [disabled]="grantingFree()" (click)="grantFreeGeneration()">{{ grantingFree() ? 'Se acordă…' : 'Oferă o generare gratuită' }}</button></div>
            @if (grantMessage()) { <p class="account-note" [class.error]="grantError()" role="status">{{ grantMessage() }}</p> }
          } @else { <p class="account-note">Contorul nu este disponibil pentru această firmă (firmă descoperită sau fără membru proprietar).</p> }
        </section>
      }
      <app-supplier-settings [companyId]="company()!.id" />
      <app-company-vault [companyId]="company()!.id" />
      <p class="account-note">Setările și oportunitățile firmei rămân în fluxurile dedicate de furnizor. Nu schimbă modul în care folosești contul personal.</p>
    } @else { <div class="profile-load-error" role="alert"><p>Firma nu este disponibilă pentru contul tău.</p><a routerLink="/account">Înapoi la cont</a></div> }
  </section>`,
  styleUrl: './company-details.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CompanyDetailsComponent {
  private readonly route = inject(ActivatedRoute); private readonly profiles = inject(ProfileService);
  private readonly auth = inject(AuthService); private readonly registry = inject(CompanyRegistryService);
  readonly company = signal<SupplierProfile | null>(null); readonly loading = signal(true);
  readonly isAdmin = computed(() => this.auth.isAdministrator());
  readonly freeGenerations = signal<number | null>(null);
  readonly grantingFree = signal(false);
  readonly grantMessage = signal('');
  readonly grantError = signal(false);
  constructor() {
    const id = this.route.snapshot.paramMap.get('companyId');
    if (!id) { this.loading.set(false); return; }
    this.profiles.getSupplier(id).subscribe({
      next: value => {
        this.company.set(value);
        this.loading.set(false);
        if (this.isAdmin() && value) this.loadFreeGenerations(value.id);
      },
      error: () => this.loading.set(false)
    });
  }

  /** Administrator-only: reads the registered-company free-generation counter (owner account). */
  private loadFreeGenerations(companyId: string): void {
    this.registry.list().subscribe({
      next: entries => {
        const entry = entries.find(item => item.kind === 'Registered' && item.id === companyId);
        this.freeGenerations.set(entry?.freeGenerationsRemaining ?? null);
      },
      error: () => this.freeGenerations.set(null)
    });
  }

  /** Administrator action: grants one more free auto-bid generation to the company's owner account. */
  grantFreeGeneration(): void {
    const company = this.company();
    if (!company || this.grantingFree()) return;
    if (!confirm(`Oferi o generare gratuită firmei „${company.tradingName}”? Contorul actual: ${this.freeGenerations() ?? 0}.`)) return;
    this.grantingFree.set(true);
    this.grantMessage.set('');
    this.grantError.set(false);
    this.registry.grantFreeGeneration(company.id).subscribe({
      next: entry => {
        this.freeGenerations.set(entry.freeGenerationsRemaining);
        this.grantingFree.set(false);
        this.grantMessage.set('Generarea gratuită a fost acordată.');
      },
      error: () => {
        this.grantingFree.set(false);
        this.grantError.set(true);
        this.grantMessage.set('Oferta gratuită nu a putut fi acordată. Încearcă din nou.');
      }
    });
  }
}
