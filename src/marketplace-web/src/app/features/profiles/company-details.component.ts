import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
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
  readonly company = signal<SupplierProfile | null>(null); readonly loading = signal(true);
  constructor() { const id = this.route.snapshot.paramMap.get('companyId'); if (!id) { this.loading.set(false); return; } this.profiles.getSupplier(id).subscribe({ next: value => { this.company.set(value); this.loading.set(false); }, error: () => this.loading.set(false) }); }
}
