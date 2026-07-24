import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { OpenRfq, OpenRfqService } from '../../core/rfqs/open-rfq.service';
import { CategoryOption, SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';

@Component({
  selector: 'app-open-rfq-list',
  imports: [RouterLink, DatePipe],
  template: `<section class="requests-shell">
    <a class="back" routerLink="/dashboard">← Înapoi la cont</a>
    <header>
      <div><p class="eyebrow">Pentru furnizori</p><h1>Cereri deschise</h1>
        <p>RFQ-uri publicate care corespund categoriilor și zonei tale de livrare.</p></div>
      <span class="result-count">{{ requests().length }} rezultate</span>
    </header>

    <section class="filters" aria-label="Filtre cereri">
      <label>Caută
        <input #searchInput type="search" placeholder="Material, proiect sau localitate"
          (keyup.enter)="load(searchInput.value, categoryInput.value)">
      </label>
      <label>Categorie
        <select #categoryInput>
          <option value="">Toate categoriile potrivite</option>
          @for (category of categories(); track category.id) {
            <option [value]="category.id">{{ category.name }}</option>
          }
        </select>
      </label>
      <button type="button" (click)="load(searchInput.value, categoryInput.value)">Aplică filtrele</button>
    </section>

    @if (loading()) { <p class="state" role="status">Se încarcă cererile…</p> }
    @if (error()) { <p class="state error" role="alert">{{ error() }}</p> }
    @if (!loading() && !error() && requests().length === 0) {
      <section class="state empty">
        <strong>Nu există cereri potrivite acum.</strong>
        <span>Verifică selecția de categorii și județele deservite din contul tău.</span>
      </section>
    }
    <section class="request-grid">
      @for (request of requests(); track request.id) {
        <article>
          <div class="card-top"><span class="category">{{ request.categoryName }}</span>
            <small>{{ request.deliveryLocality }}, {{ request.county }}</small></div>
          <h2>{{ request.title }}</h2>
          <p>{{ request.description || request.projectName || 'Cerere de materiale pentru construcții' }}</p>
          <dl>
            <div><dt>Materiale</dt><dd>{{ request.items.length || 'Listă generală' }}</dd></div>
            <div><dt>Termen</dt><dd>{{ request.quotationDeadline | date:'dd.MM.yyyy, HH:mm' }}</dd></div>
            @if (request.estimatedBudget !== null) {
              <div><dt>Buget estimat</dt><dd>{{ request.estimatedBudget }} lei</dd></div>
            }
          </dl>
          <a class="primary-link" [routerLink]="['/open-requests', request.id]">Vezi cererea</a>
        </article>
      }
    </section>
  </section>`,
  styleUrl: './open-rfq-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OpenRfqListComponent {
  private readonly service = inject(OpenRfqService);
  private readonly categoryService = inject(SupplierSettingsService);
  readonly auth = inject(AuthService);
  readonly requests = signal<OpenRfq[]>([]);
  readonly categories = signal<CategoryOption[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');

  constructor() {
    this.categoryService.getCategories().subscribe({ next: values => this.categories.set(values.filter(value => value.parentId !== null)) });
    this.load();
  }

  load(search = '', categoryId = ''): void {
    this.loading.set(true); this.error.set('');
    this.service.list(search, categoryId).subscribe({
      next: values => { this.requests.set(values); this.loading.set(false); },
      error: error => {
        this.loading.set(false);
        this.error.set(error.status === 403
          ? 'Contul curent nu are rolul Furnizor.'
          : 'Cererile nu au putut fi încărcate. Verifică dacă API-ul rulează.');
      }
    });
  }
}
