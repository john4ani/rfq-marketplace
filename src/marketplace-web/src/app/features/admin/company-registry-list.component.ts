import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CompanyRegistryEntry, CompanyRegistryService } from '../../core/companies/company-registry.service';
import { SupplierDiscoveryService, SupplierWebDiscoveryRun } from '../../core/supplier-discovery/supplier-discovery.service';
import { CategoryOption, SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';

@Component({
  selector: 'app-company-registry-list',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`.page{display:grid;gap:1.15rem;max-width:76rem}.header-rule{display:flex;align-items:end;justify-content:space-between;gap:1.5rem}.eyebrow{margin:0;color:var(--forest-light);font-size:.76rem;font-weight:800;letter-spacing:.1em;text-transform:uppercase}h1{margin:.15rem 0;font-size:clamp(2rem,4vw,2.9rem);letter-spacing:-.04em}.header-rule>div>p:last-child{margin:.3rem 0 0;max-width:38rem;color:var(--muted)}.count{margin:0;padding:.45rem .9rem;border:1px solid var(--border);border-radius:999px;background:var(--surface);color:var(--muted);font-size:.85rem;font-weight:750;white-space:nowrap}.toolbar{display:flex;align-items:center;gap:.75rem;flex-wrap:wrap}.search{position:relative;flex:1 1 20rem;min-width:14rem}.search input{width:100%;min-height:2.75rem;padding:.55rem 2.4rem .55rem .9rem;border:1px solid var(--border);border-radius:.7rem;background:var(--surface);font:inherit}.search input:focus{outline:2px solid color-mix(in srgb,var(--forest-light) 40%,transparent);outline-offset:-1px;border-color:var(--forest-light)}.search::after{position:absolute;top:50%;right:.95rem;transform:translateY(-50%);color:var(--muted);content:'⌕';pointer-events:none}.filters{display:flex;gap:.35rem;padding:.25rem;border:1px solid var(--border);border-radius:.8rem;background:color-mix(in srgb,var(--sand) 38%,white)}.filters button{padding:.5rem .85rem;border:0;border-radius:.6rem;color:var(--muted);background:transparent;font:inherit;font-size:.88rem;font-weight:700;cursor:pointer}.filters button.active{color:#fff;background:var(--forest)}.card,.state,.external{padding:1.1rem 1.2rem;border:1px solid var(--border);border-radius:1rem;background:var(--surface)}.company-card{display:flex;align-items:center;justify-content:space-between;gap:1.25rem;transition:border-color .15s,box-shadow .15s}.company-card:hover{border-color:color-mix(in srgb,var(--forest) 45%,var(--border));box-shadow:var(--shadow-soft)}.company-main{display:grid;gap:.35rem;min-width:0}.company-top{display:flex;align-items:center;gap:.6rem;flex-wrap:wrap}.company-top h2{margin:0;font-size:1.15rem;letter-spacing:-.015em}.pill{display:inline-flex;align-items:center;padding:.28rem .68rem;border-radius:999px;font-size:.76rem;font-weight:800}.pill.registered{color:var(--forest);background:color-mix(in srgb,var(--forest-light) 13%,white)}.pill.inactive{color:#6b4a44;background:#f0eae6}.pill.discovered{color:#7a5206;background:#faf0d8}.pill.claimed{color:var(--muted);background:#efede7}.company-meta{color:var(--muted);font-size:.88rem}.categories{display:flex;flex-wrap:wrap;gap:.4rem;align-items:center}.category-chip{display:inline-flex;padding:.28rem .68rem;border-radius:999px;color:#4c5a52;background:color-mix(in srgb,var(--sand) 50%,white);font-size:.8rem;font-weight:600}.arrow-link{display:inline-flex;flex:0 0 auto;align-items:center;gap:.4rem;padding:.65rem .95rem;border-radius:.6rem;color:var(--forest);background:color-mix(in srgb,var(--sand) 40%,white);font-weight:800;text-decoration:none;white-space:nowrap}.arrow-link:hover{background:color-mix(in srgb,var(--sand) 70%,white)}.state{display:grid;justify-items:center;gap:.45rem;padding:2rem;text-align:center;color:var(--muted)}.state.error{border-color:#edc9c6;color:#78231d;background:#fff7f5}.state strong{color:var(--ink)}.external{display:grid;gap:.8rem;border-color:#e2c98f;background:color-mix(in srgb,var(--sand) 55%,white)}.external>strong{font-size:1.02rem}.external>p{margin:0;color:var(--muted);line-height:1.5}.external-form{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:.75rem}.external-form label{display:grid;gap:.3rem;font-weight:650}.external-form input,.external-form select{min-height:2.6rem;padding:.5rem .7rem;border:1px solid var(--border);border-radius:.55rem;background:#fff;font:inherit}.external-actions{display:flex;gap:.6rem;flex-wrap:wrap}button.primary{min-height:2.6rem;padding:.6rem 1.05rem;border:0;border-radius:.6rem;color:#fff;background:var(--forest);font:inherit;font-weight:800;cursor:pointer}button.primary:disabled{opacity:.55;cursor:not-allowed}button.secondary{min-height:2.6rem;padding:.6rem 1.05rem;border:1px solid var(--forest);border-radius:.6rem;color:var(--forest);background:transparent;font:inherit;font-weight:800;cursor:pointer}.note{border-radius:.7rem;padding:.8rem .95rem;color:#4c3d10;background:#fdf3d8;line-height:1.5}.note[role="alert"]{color:#8b2222;background:#fff0f0}.sources-note{display:flex;flex-wrap:wrap;align-items:center;gap:.5rem}.sources-note button{min-height:2.4rem;padding:.45rem .9rem;border:1px solid var(--forest);border-radius:.55rem;color:var(--forest);background:transparent;font:inherit;font-weight:800;cursor:pointer}.candidate-card{display:grid;gap:.4rem;padding:.95rem 1.05rem;border:1px solid var(--border);border-radius:.8rem;background:var(--warm-white)}.candidate-card>strong{color:#7a5206;font-size:.78rem;text-transform:uppercase;letter-spacing:.05em}.candidate-card h3{margin:0;font-size:1.02rem}.candidate-card p{margin:0;color:var(--muted);font-size:.9rem;line-height:1.45}.candidate-card button{justify-self:start}@media(max-width:46rem){.header-rule{align-items:stretch;flex-direction:column}.company-card{align-items:stretch;flex-direction:column}.arrow-link{justify-content:center}.external-form{grid-template-columns:1fr}}`],
  template: `<section class="page">
    <header class="header-rule"><div><p class="eyebrow">Administrare</p><h1>Firme</h1><p>Firme înregistrate și firme descoperite de CompaSpot.</p></div>
      @if (!loading() && !error()) { <p class="count">{{ filtered().length }} {{ filtered().length === 1 ? 'firmă' : 'de firme' }}</p> }</header>
    <div class="toolbar"><div class="search"><input type="search" [value]="query()" (input)="query.set($any($event.target).value)" placeholder="Caută firmă" aria-label="Caută firmă"></div>
      <div class="filters">@for(item of filters;track item){<button type="button" [class.active]="filter()===item" (click)="filter.set(item)">{{label(item)}}</button>}</div></div>
    @if(loading()){<section class="state" role="status">Se încarcă firmele…</section>}
    @else if(error()){<section class="state error" role="alert"><strong>Nu am putut încărca firmele.</strong><button type="button" class="secondary" (click)="load()">Încearcă din nou</button></section>}
    @else{
      @for(company of filtered();track company.kind+'-'+company.id){
        <article class="card company-card">
          <div class="company-main">
            <div class="company-top"><h2>{{company.name}}</h2><span class="pill" [class]="pillClass(company)">{{status(company)}}</span></div>
            <p class="company-meta">{{company.locality}}{{company.county?', '+company.county:''}}</p>
            <div class="categories">@if(company.categories.length){@for(category of company.categories;track category){<span class="category-chip">{{category}}</span>}}@else{<span class="company-meta">Fără categorii declarate</span>}</div>
          </div>
          <a class="arrow-link" [routerLink]="['/admin/companies',company.kind,company.id]">Vezi firma →</a>
        </article>
      }
      @empty{
        <section class="external">
          <strong>Nu găsim firma în CompaSpot.</strong><p>Caută exact firma extern. Rezultatele rămân neverificate până le confirmi.</p>
          <div class="external-form">
            <label>Localitate <input [value]="locality()" (input)="locality.set($any($event.target).value)"></label>
            <label>Județ <input [value]="county()" (input)="county.set($any($event.target).value)"></label>
            <label>Categorie <select (change)="categoryId.set($any($event.target).value)"><option value="">Selectează…</option>@for(category of categories();track category.id){<option [value]="category.id">{{category.name}}</option>}</select></label>
          </div>
          <div class="external-actions"><button type="button" class="primary" [disabled]="searching()||query().trim().length<2" (click)="searchExternal()">{{searching()?'Se caută…':'Caută și extern'}}</button></div>
          @if(searchError()){<p class="note" role="alert">{{searchError()}}</p>}
          @if(run();as result){
            @if(result.status==='EvidenceReady'){
              <div class="note sources-note">Dovezi provizorii: @for(source of result.sources;track source.source){ <strong>{{source.source}}</strong>: {{source.resultCount}} rezultate. }<button type="button" (click)="qualify(result.id)">Califică rezultatele</button></div>
            }@else if(result.status==='Qualified'){
              @for(candidate of result.candidates;track candidate.companyName;let index=$index){
                <article class="candidate-card"><strong>Găsită extern · neverificată</strong><h3>{{candidate.companyName}}</h3><p>{{candidate.locality}}, {{candidate.county}}</p><p>{{candidate.whyRelevant}}</p><button type="button" class="primary" [disabled]="!categoryId()" (click)="accept(result.id,index)">Adaugă drept firmă candidat</button></article>
              }@empty{<p class="note">Nu au fost găsite firme suficient de relevante.</p>}
            }@else{<p class="note" role="alert">{{result.failureMessage}}</p>}
          }
        </section>
      }
    }
  </section>`})
export class CompanyRegistryListComponent {
  private readonly api = inject(CompanyRegistryService);
  private readonly discovery = inject(SupplierDiscoveryService);
  private readonly settings = inject(SupplierSettingsService);
  readonly companies = signal<CompanyRegistryEntry[]>([]);
  readonly categories = signal<CategoryOption[]>([]);
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly searching = signal(false);
  readonly searchError = signal('');
  readonly filter = signal('all');
  readonly query = signal('');
  readonly locality = signal('');
  readonly county = signal('');
  readonly categoryId = signal('');
  readonly run = signal<SupplierWebDiscoveryRun | null>(null);
  readonly filters = ['all', 'registered', 'discovered'];
  readonly filtered = computed(() => {
    const q = this.query().trim().toLocaleLowerCase('ro');
    return this.companies().filter(company =>
      (this.filter() === 'all'
        || this.filter() === 'registered' && company.kind === 'Registered'
        || this.filter() === 'discovered' && company.kind === 'Discovered')
      && (!q || company.name.toLocaleLowerCase('ro').includes(q)));
  });

  constructor() {
    this.load();
    this.settings.getCategories().subscribe({ next: value => this.categories.set(value) });
  }

  load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.api.list().subscribe({
      next: value => { this.companies.set(value); this.loading.set(false); },
      error: () => { this.error.set(true); this.loading.set(false); }
    });
  }

  searchExternal(): void {
    this.searching.set(true);
    this.searchError.set('');
    this.run.set(null);
    this.discovery.discoverCompanyOnWeb({
      companyName: this.query().trim(),
      locality: this.locality().trim() || undefined,
      county: this.county().trim() || undefined,
      categoryIds: this.categoryId() ? [this.categoryId()] : []
    }).subscribe({
      next: run => { this.run.set(run); this.searching.set(false); },
      error: () => { this.searchError.set('Nu am putut căuta firma extern. Încearcă din nou.'); this.searching.set(false); }
    });
  }

  qualify(id: string): void {
    this.searching.set(true);
    this.discovery.qualifyWebDiscovery(id).subscribe({
      next: run => { this.run.set(run); this.searching.set(false); },
      error: () => { this.searchError.set('Nu am putut califica rezultatele externe.'); this.searching.set(false); }
    });
  }

  accept(id: string, index: number): void {
    this.searching.set(true);
    this.discovery.acceptWebCandidate(id, index, [this.categoryId()]).subscribe({
      next: () => { this.searching.set(false); this.load(); },
      error: () => { this.searchError.set('Firma nu a putut fi adăugată. Verifică categoria și încearcă din nou.'); this.searching.set(false); }
    });
  }

  label(value: string): string {
    return ({ all: 'Toate', registered: 'Înregistrate', discovered: 'Descoperite' } as Record<string, string>)[value];
  }

  status(company: CompanyRegistryEntry): string {
    return company.kind === 'Registered' ? (company.isActive ? 'Înregistrată' : 'Înregistrată · Dezactivată') : company.claimed ? 'Revendicată' : 'Descoperită · Nerevendicată';
  }

  pillClass(company: CompanyRegistryEntry): string {
    return company.kind === 'Registered' ? (company.isActive ? 'registered' : 'inactive') : company.claimed ? 'claimed' : 'discovered';
  }
}
