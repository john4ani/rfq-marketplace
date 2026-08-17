import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ProfileService, RepresentedCompany } from '../../core/profiles/profile.service';

@Component({
  selector: 'app-represented-companies',
  imports: [RouterLink],
  template: `<section class="profile-card companies-card" aria-labelledby="companies-title">
    <div class="section-heading"><div><p class="eyebrow">Firme</p><h2 id="companies-title">Firmele pe care le reprezinți</h2></div>
      <p>O firmă rămâne separată de contul tău personal. O alegi doar când o acțiune are nevoie de acest context.</p></div>
    @if (loading()) { <p role="status">Se încarcă firmele…</p> }
    @else if (failed()) { <div class="profile-load-error" role="alert"><p>Firmele nu au putut fi încărcate acum.</p><button type="button" (click)="load()">Încearcă din nou</button></div> }
    @else if (!companies().length) { <div class="empty-state"><div><strong>Nu reprezinți încă nicio firmă în CompaSpot.</strong><p>Poți continua să cumperi sau îți poți adăuga firma pentru a vedea oportunități relevante.</p></div><a routerLink="/account/company/new">Adaugă firma</a></div> }
    @else { <ul class="company-list">@for (company of companies(); track company.id) { <li><div><strong>{{ company.name }}</strong><span>{{ company.role }}</span></div><a [routerLink]="['/account/company', company.id]">Vezi firma</a></li> }</ul> }
  </section>`,
  styleUrl: './represented-companies.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RepresentedCompaniesComponent {
  private readonly profiles = inject(ProfileService);
  readonly companies = signal<RepresentedCompany[]>([]); readonly loading = signal(true); readonly failed = signal(false);
  constructor() { this.load(); }
  load(): void { this.loading.set(true); this.failed.set(false); this.profiles.getMyCompanies().subscribe({ next: values => { this.companies.set(values); this.loading.set(false); }, error: () => { this.loading.set(false); this.failed.set(true); } }); }
}
