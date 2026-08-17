import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { OpenRfq, OpenRfqService } from '../../core/rfqs/open-rfq.service';
import { CategoryOption, SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';
import { ProfileService, RepresentedCompany } from '../../core/profiles/profile.service';
import { TrustFactsComponent } from '../profiles/trust-facts.component';

@Component({
  selector: 'app-open-rfq-list',
  imports: [RouterLink, DatePipe, TrustFactsComponent],
  template: `<section class="requests-shell">
    <a class="back" routerLink="/account">← Înapoi la firmă</a>
    <header>
      <div><p class="eyebrow">Firma ta</p><h1>Oportunități<span class="desktop-company"> pentru {{ selectedCompanyName() || 'firma ta' }}</span></h1>
        @if (selectedCompanyName()) { <p class="mobile-company">{{ selectedCompanyName() }}</p> }
        <p>Vezi cererile relevante pentru ce oferă firma și zona în care livrează.</p></div>
      @if (resolvedCompanyId()) { <div class="header-actions">
        <span class="result-count">{{ requests().length }} rezultate</span>
        <a class="company-offers-link" routerLink="/company/offers" [queryParams]="{ companyId: resolvedCompanyId() }">Ofertele firmei</a>
      </div> }
    </header>

    @if (!noCompany() && !selectingCompany()) {
    <section class="filters" aria-label="Filtre cereri">
      <label>Caută
        <input #searchInput type="search" placeholder="Produs, proiect sau localitate" (keyup.enter)="load(searchInput.value, categoryInput.value)">
      </label>
      <label>Categorie
        <select #categoryInput (change)="load(searchInput.value, categoryInput.value)">
          <option value="">Toate categoriile potrivite</option>
          @for (category of categories(); track category.id) { <option [value]="category.id">{{ category.name }}</option> }
        </select>
      </label>
    </section>
    @if (categoryError()) { <p class="filter-warning" role="status">{{ categoryError() }}</p> }
    @if (profileGuidance()) {
      <aside class="profile-guidance" role="status"><div><strong>Completează profilul firmei</strong><p>{{ profileGuidance() }}</p></div><a [routerLink]="companySettingsLink()">Completează</a></aside>
    }
    <aside class="matching-explanation"><span aria-hidden="true">✓</span><div><strong>De ce vezi aceste cereri?</strong>
      <p>Se potrivesc cu cel puțin o categorie din profilul firmei și cu zona în care ai spus că poți livra. O potrivire parțială arată numai pozițiile relevante pentru specializarea ta.</p></div>
      <a [routerLink]="companySettingsLink()">Verifică preferințele</a></aside>
    }

    @if (noCompany()) {
      <section class="state empty"><span class="empty-icon" aria-hidden="true">⌕</span><div><strong>Nu reprezinți încă nicio firmă în CompaSpot.</strong><span>Poți continua să cumperi sau îți poți adăuga firma pentru a vedea oportunități.</span></div><a routerLink="/account/company/new">Adaugă firma</a></section>
    } @else if (selectingCompany()) {
      <section class="state empty"><div><strong>Pentru ce firmă vrei să vezi oportunitățile?</strong><span>Alege firma doar pentru această sesiune de lucru.</span></div>
        @for (company of companies(); track company.id) { <a [routerLink]="'/opportunities'" [queryParams]="{ companyId: company.id }">{{ company.name }}</a> }
      </section>
    } @else if (loading()) {
      <div class="loading-grid" role="status" aria-label="Încărcăm oportunitățile"><span>Încărcăm oportunitățile…</span><span></span><span></span></div>
    } @else if (error()) {
      <section class="state error" role="alert"><div><strong>Nu am putut încărca oportunitățile.</strong><span>{{ error() }}</span></div><button type="button" (click)="load()">Încearcă din nou</button></section>
    } @else if (requests().length === 0) {
      <section class="state empty"><span class="empty-icon" aria-hidden="true">⌕</span><div><strong>Nu sunt oportunități potrivite pentru firma ta momentan.</strong>
        <span>Te vom anunța când apare o cerere relevantă.</span></div>@if (profileGuidance()) { <a [routerLink]="companySettingsLink()">Verifică ce poate oferi firma</a> }</section>
    } @else {
      <section class="request-grid">
        @for (request of requests(); track request.id) {
          <article>
            <div class="card-top"><span class="category">{{ request.isPartialMatch ? 'Potrivire specializată' : request.categoryName }}</span>
              <span class="deadline" [class.urgent]="daysRemaining(request.quotationDeadline) <= 2">{{ deadlineLabel(request.quotationDeadline) }}</span></div>
            <h2>{{ request.title }}</h2>
            <p class="buyer">{{ request.buyerDisplayName }} · {{ request.deliveryLocality }}, {{ request.county }}</p>
            <p class="opportunity-state">Cerere activă@if (request.deliveryRequired) { · Livrare solicitată }</p>
            <p class="match-reason"><span aria-hidden="true">✓</span><strong>Potrivire după {{ request.categoryName }}</strong> și zona {{ request.county }}</p>
            <p class="description">{{ request.description || request.projectName || 'Cerere generală de ofertă' }}</p>
            <dl>
              <div><dt>Poziții</dt><dd>{{ request.items.length || 'Cerere generală' }}</dd></div>
              <div><dt>Termen</dt><dd>{{ request.quotationDeadline | date:'dd.MM, HH:mm' }}</dd></div>
              @if (request.estimatedBudget !== null) { <div><dt>Buget estimat</dt><dd>{{ request.estimatedBudget }} lei</dd></div> }
            </dl><app-trust-facts [facts]="request.buyerTrustFacts" role="Buyer" />
            <a class="primary-link" [routerLink]="['/opportunities', request.id]" [queryParams]="companyId ? { companyId } : null">Vezi cererea →</a>
          </article>
        }
      </section>
    }
  </section>`,
  styleUrl: './open-rfq-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OpenRfqListComponent {
  private readonly service = inject(OpenRfqService);
  private readonly categoryService = inject(SupplierSettingsService);
  private readonly profiles = inject(ProfileService);
  readonly requests = signal<OpenRfq[]>([]);
  readonly categories = signal<CategoryOption[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly categoryError = signal('');
  readonly profileGuidance = signal('');
  readonly companyId = inject(ActivatedRoute).snapshot.queryParamMap.get('companyId') ?? '';
  readonly companies = signal<RepresentedCompany[]>([]);
  readonly selectedCompanyName = signal('');
  readonly selectingCompany = signal(false);
  readonly noCompany = signal(false);
  readonly resolvedCompanyId = computed(() => this.companyId || (this.companies().length === 1 ? this.companies()[0].id : ''));
  readonly companySettingsLink = computed(() => {
    const companyId = this.resolvedCompanyId();
    return companyId ? ['/account/company', companyId] : ['/account'];
  });

  constructor() {
    this.categoryService.getCategories().subscribe({
      next: values => this.categories.set(values.filter(value => value.parentId !== null)),
      error: () => this.categoryError.set('Filtrarea pe categorie nu este disponibilă momentan. Poți vedea în continuare toate cererile potrivite.')
    });
    this.profiles.getMyCompanies().subscribe({
      next: companies => {
        this.companies.set(companies);
        this.selectedCompanyName.set(companies.find(company => company.id === this.companyId)?.name ?? (companies.length === 1 ? companies[0].name : ''));
        this.noCompany.set(companies.length === 0);
        this.selectingCompany.set(!this.companyId && companies.length > 1);
        const resolvedCompanyId = this.companyId || (companies.length === 1 ? companies[0].id : '');
        if (resolvedCompanyId) { this.loadCompanyCompleteness(resolvedCompanyId); this.load(); }
      },
      error: () => this.load()
    });
  }

  private loadCompanyCompleteness(companyId: string): void {
    forkJoin({
      categories: this.categoryService.getSupplierCategories(companyId),
      delivery: this.categoryService.getDeliverySettings(companyId)
    }).subscribe({
      next: ({ categories, delivery }) => {
        if (categories.categoryIds.length === 0) {
          this.profileGuidance.set('Adaugă ce produse sau servicii oferă firma pentru a primi oportunități relevante.');
        } else if (!delivery.nationwideDelivery && delivery.counties.length === 0) {
          this.profileGuidance.set('Completează zona în care lucrează firma pentru a primi oportunități relevante.');
        }
      }
    });
  }

  daysRemaining(deadline: string): number { return Math.ceil((new Date(deadline).getTime() - Date.now()) / 86_400_000); }
  deadlineLabel(deadline: string): string {
    const days = this.daysRemaining(deadline);
    return days <= 0 ? 'Ultimele ore' : days === 1 ? 'Expiră mâine' : days <= 7 ? `Expiră în ${days} zile` : 'Deschisă';
  }

  load(search = '', categoryId = ''): void {
    this.loading.set(true); this.error.set('');
    this.service.list(search, categoryId, this.companyId).subscribe({
      next: values => { this.requests.set(values); this.loading.set(false); },
      error: error => {
        this.loading.set(false);
        this.error.set(error.status === 409 ? 'Alege firma pentru care vrei să vezi oportunitățile.' : 'Încearcă din nou în câteva momente.');
      }
    });
  }
}
