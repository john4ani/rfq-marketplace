import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { OpenRfq, OpenRfqService } from '../../core/rfqs/open-rfq.service';
import { CategoryOption, SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';
import { TrustFactsComponent } from '../profiles/trust-facts.component';

@Component({
  selector: 'app-open-rfq-list',
  imports: [RouterLink, DatePipe, TrustFactsComponent],
  template: `<section class="requests-shell">
    <a class="back" routerLink="/">← Înapoi la Piața Live</a>
    <header>
      <div><p class="eyebrow">Oportunități potrivite</p><h1>Cereri deschise</h1>
        <p>Explorează cererile relevante pentru categoriile și zona ta de livrare.</p></div>
      <div class="header-actions">
        <span class="result-count">{{ requests().length }} rezultate</span>
        @if (hasBuyerProfile()) {
          <a class="create-request" routerLink="/rfqs/new">+ Creează o cerere</a>
        }
      </div>
    </header>

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
    <aside class="matching-explanation"><span aria-hidden="true">✓</span><div><strong>De ce vezi aceste cereri?</strong>
      <p>Se potrivesc cu cel puțin o categorie din profilul firmei și cu zona în care ai spus că poți livra. O potrivire parțială arată numai pozițiile relevante pentru specializarea ta.</p></div>
      <a routerLink="/account">Verifică preferințele</a></aside>

    @if (loading()) {
      <div class="loading-grid" role="status" aria-label="Se încarcă cererile"><span></span><span></span><span></span></div>
    } @else if (error()) {
      <section class="state error" role="alert"><div><strong>Cererile nu sunt disponibile momentan.</strong><span>{{ error() }}</span></div><button type="button" (click)="load()">Încearcă din nou</button></section>
    } @else if (requests().length === 0) {
      <section class="state empty"><span class="empty-icon" aria-hidden="true">⌕</span><div><strong>Nu există cereri potrivite acum.</strong>
        <span>Poți ajusta categoriile și județele deservite pentru a primi potriviri mai bune.</span></div>
        <a routerLink="/dashboard">Verifică preferințele</a></section>
    } @else {
      <section class="request-grid">
        @for (request of requests(); track request.id) {
          <article>
            <div class="card-top"><span class="category">{{ request.isPartialMatch ? 'Potrivire specializată' : request.categoryName }}</span>
              <span class="deadline" [class.urgent]="daysRemaining(request.quotationDeadline) <= 2">{{ deadlineLabel(request.quotationDeadline) }}</span></div>
            <h2>{{ request.title }}</h2>
            <p class="buyer">{{ request.buyerDisplayName }} · {{ request.deliveryLocality }}, {{ request.county }}</p>
            <p class="match-reason"><span aria-hidden="true">✓</span><strong>Potrivire după {{ request.categoryName }}</strong> și zona {{ request.county }}</p>
            <p class="description">{{ request.description || request.projectName || 'Cerere generală de ofertă' }}</p>
            <dl>
              <div><dt>Poziții</dt><dd>{{ request.items.length || 'Cerere generală' }}</dd></div>
              <div><dt>Termen</dt><dd>{{ request.quotationDeadline | date:'dd.MM, HH:mm' }}</dd></div>
              @if (request.estimatedBudget !== null) { <div><dt>Buget estimat</dt><dd>{{ request.estimatedBudget }} lei</dd></div> }
            </dl><app-trust-facts [facts]="request.buyerTrustFacts" role="Buyer" />
            <a class="primary-link" [routerLink]="['/open-requests', request.id]">Vezi detalii și oferă →</a>
          </article>
        }
      </section>
    }
  </section>`,
  styleUrl: './open-rfq-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OpenRfqListComponent {
  private readonly auth = inject(AuthService);
  private readonly service = inject(OpenRfqService);
  private readonly categoryService = inject(SupplierSettingsService);
  readonly hasBuyerProfile = computed(() => this.auth.profiles().some(profile => profile.accountType === 'Buyer'));
  readonly requests = signal<OpenRfq[]>([]);
  readonly categories = signal<CategoryOption[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');

  constructor() {
    this.categoryService.getCategories().subscribe({ next: values => this.categories.set(values.filter(value => value.parentId !== null)) });
    this.load();
  }

  daysRemaining(deadline: string): number { return Math.ceil((new Date(deadline).getTime() - Date.now()) / 86_400_000); }
  deadlineLabel(deadline: string): string {
    const days = this.daysRemaining(deadline);
    return days <= 0 ? 'Ultimele ore' : days === 1 ? 'Expiră mâine' : days <= 7 ? `Expiră în ${days} zile` : 'Deschisă';
  }

  load(search = '', categoryId = ''): void {
    this.loading.set(true); this.error.set('');
    this.service.list(search, categoryId).subscribe({
      next: values => { this.requests.set(values); this.loading.set(false); },
      error: error => {
        this.loading.set(false);
        this.error.set(error.status === 403 ? 'Contul curent nu are acces la cererile furnizorilor.' : 'Cererile nu au putut fi încărcate. Verifică dacă API-ul rulează.');
      }
    });
  }
}
