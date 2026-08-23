import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormControl, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CompanyRegistryEntry, CompanyRegistryService } from '../../core/companies/company-registry.service';
import { CategoryOption, SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';

@Component({
  selector: 'app-company-detail-editor',
  imports: [RouterLink, ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`.page{display:grid;gap:1.4rem;max-width:52rem}.back-link{display:inline-flex;align-items:center;gap:.4rem;width:fit-content;color:var(--forest);font-weight:750;text-decoration:none;font-size:.92rem}.back-link:hover{text-decoration:underline}.state{padding:2rem;border:1px solid var(--border);border-radius:1rem;background:var(--surface);text-align:center;color:var(--muted)}.page-header{display:grid;gap:.5rem}.kind-mark{display:flex;align-items:center;gap:.8rem}.kind-mark>span{display:grid;width:2.7rem;height:2.7rem;flex:0 0 auto;place-items:center;border-radius:.9rem;color:var(--forest);background:color-mix(in srgb,var(--sand) 72%,white);font-size:1.2rem;font-weight:850}.eyebrow{margin:0;color:var(--forest-light);font-size:.76rem;font-weight:800;letter-spacing:.1em;text-transform:uppercase}h1{margin:.15rem 0 0;font-size:clamp(1.9rem,4vw,2.7rem);line-height:1.06;letter-spacing:-.035em}.page-header>p{display:flex;flex-wrap:wrap;align-items:center;gap:.6rem;margin:.15rem 0 0;color:var(--muted);font-size:.95rem}.pill{display:inline-flex;align-items:center;gap:.35rem;padding:.32rem .75rem;border-radius:999px;font-size:.78rem;font-weight:800}.pill.registered{color:var(--forest);background:color-mix(in srgb,var(--forest-light) 13%,white)}.pill.inactive{color:#6b4a44;background:#f0eae6}.pill.discovered{color:#7a5206;background:#faf0d8}.pill.claimed{color:var(--muted);background:#efede7}.card{display:grid;gap:1rem;padding:1.25rem;border:1px solid var(--border);border-radius:1rem;background:var(--surface);box-shadow:var(--shadow-soft)}.card h2{margin:0;font-size:1.1rem}.card>p{margin:0;color:var(--muted);line-height:1.5}.facts{display:grid;grid-template-columns:repeat(auto-fit,minmax(11rem,1fr));gap:.75rem}.facts>div{display:grid;gap:.2rem;padding:.85rem;border:1px solid var(--border);border-radius:.8rem;background:var(--warm-white)}.facts span{color:var(--muted);font-size:.74rem;font-weight:750;text-transform:uppercase;letter-spacing:.05em}.facts strong{font-size:.98rem;font-weight:750;overflow-wrap:anywhere}.chips{display:flex;flex-wrap:wrap;gap:.45rem}.chip{display:inline-flex;align-items:center;padding:.38rem .78rem;border-radius:999px;color:var(--forest);background:color-mix(in srgb,var(--sand) 62%,white);font-size:.86rem;font-weight:650}.actions{display:flex;flex-wrap:wrap;gap:.7rem}.link-row{display:flex;flex-wrap:wrap;gap:1.1rem}.link-row a{display:inline-flex;align-items:center;gap:.35rem;color:var(--forest);font-weight:800;text-decoration:none}.link-row a:hover{text-decoration:underline}.form-grid{display:grid;grid-template-columns:1fr 1fr;gap:.85rem}.field{display:grid;gap:.35rem;font-weight:650}.field small{color:var(--muted);font-weight:500}.field input{min-height:2.7rem;padding:.55rem .75rem;border:1px solid var(--border);border-radius:.55rem;background:#fff;font:inherit;transition:border-color .15s}.field input:focus{outline:2px solid color-mix(in srgb,var(--forest-light) 40%,transparent);outline-offset:-1px;border-color:var(--forest-light)}.field input.ng-invalid.ng-touched{border-color:var(--danger)}.error{color:var(--danger);font-size:.85rem}fieldset{border:0;padding:0;margin:0}legend{font-weight:650;margin-bottom:.5rem}.categories{display:flex;flex-wrap:wrap;gap:.45rem}.categories label{display:inline-flex;align-items:center;gap:.4rem;padding:.5rem .8rem;border:1px solid var(--border);border-radius:999px;color:var(--ink);background:#fff;font-weight:550;cursor:pointer;transition:all .15s}.categories label:has(input:checked){border-color:var(--forest-light);color:var(--forest);background:color-mix(in srgb,var(--sand) 55%,white);font-weight:750}.categories input{accent-color:var(--forest);cursor:pointer}button{min-height:2.6rem;padding:.6rem 1.05rem;border:0;border-radius:.6rem;color:#fff;background:var(--forest);font:inherit;font-weight:800;cursor:pointer}button:disabled{opacity:.55;cursor:not-allowed}button.secondary{color:var(--forest);background:transparent;border:1px solid var(--forest)}button.danger{color:var(--danger);background:transparent;border:1px solid color-mix(in srgb,var(--danger) 50%,transparent)}button.danger:hover:not(:disabled){background:#fdf0ee;border-color:var(--danger)}.confirm{display:grid;gap:.75rem;padding:1.15rem 1.25rem;border:1px solid #e5c9c4;border-radius:1rem;background:#fff8f6}.confirm strong{font-size:1.02rem}.confirm p{margin:0;color:var(--muted);line-height:1.5}.confirm .actions{justify-content:flex-end}.confirm button.danger{color:#fff;background:var(--danger);border-color:var(--danger)}@media(max-width:44rem){.form-grid{grid-template-columns:1fr}.kind-mark{align-items:flex-start}.confirm .actions button{flex:1}}`],
  template: `
  <section class="page">
    <a class="back-link" routerLink="/admin/companies">← Înapoi la firme</a>
    @if (loading()) { <section class="state" role="status">Se încarcă firma…</section> }
    @else if (company(); as item) {
      <header class="page-header">
        <div class="kind-mark"><span aria-hidden="true">{{ kindMark(item) }}</span>
          <div><p class="eyebrow">Administrare · Firme</p><h1>{{ item.name }}</h1></div></div>
        <p><span class="pill" [class]="statusPill(item)">{{ status(item) }}</span>
          @if (item.kind === 'Registered' && !item.isActive) { <span>Dezactivată — nu primește potriviri noi</span> }
          @if (item.locality) { <span>{{ item.locality }}{{ item.county ? ', ' + item.county : '' }}</span> }
          @if (item.taxIdentifier) { <span>CUI {{ item.taxIdentifier }}</span> }
        </p>
      </header>
      @if (saveError() && !editing()) { <p class="error" role="alert">{{ saveError() }}</p> }
      @if (!editing()) {
        <section class="card">
          <div class="facts">
            @if (item.taxIdentifier) { <div><span>CUI</span><strong>{{ item.taxIdentifier }}</strong></div> }
            <div><span>Localitate</span><strong>{{ item.locality }}{{ item.county ? ', ' + item.county : '' }}</strong></div>
            @if (item.phone) { <div><span>Telefon</span><strong>{{ item.phone }}</strong></div> }
            @if (item.email) { <div><span>E-mail</span><strong>{{ item.email }}</strong></div> }
            @if (item.website) { <div><span>Website</span><strong>{{ item.website }}</strong></div> }
            @if (item.contactState) { <div><span>Contact</span><strong>{{ item.contactState }}</strong></div> }
          </div>
        </section>
        <section class="card"><h2>Ce pare să ofere</h2>
          @if (item.categories.length) { <div class="chips">@for (category of item.categories; track category) { <span class="chip">{{ category }}</span> }</div> }
          @else { <p>Nu avem încă informații despre categorii.</p> }
        </section>
        @if (item.kind === 'Registered') {
          <div class="actions"><button type="button" class="secondary" [disabled]="activeSaving()" (click)="toggleActive(item)">{{ activeSaving() ? 'Se salvează…' : item.isActive ? 'Dezactivează firma' : 'Activează firma' }}</button></div>
        }
        @if (item.kind === 'Discovered') {
          @if (!item.claimed) {
            @if (confirmingDelete()) {
              <section class="confirm" role="alertdialog" aria-labelledby="delete-title" aria-describedby="delete-copy">
                <strong id="delete-title">Ștergi firma „{{ item.name }}”?</strong>
                <p id="delete-copy">Se șterg și potrivirile ei, atâta timp cât nu au fost contactate. Acțiunea nu poate fi anulată.</p>
                <div class="actions"><button type="button" class="secondary" [disabled]="deleting()" (click)="cancelDelete()">Renunță</button>
                  <button type="button" class="danger" [disabled]="deleting()" (click)="confirmDelete(item)">{{ deleting() ? 'Se șterge…' : 'Da, șterge firma' }}</button></div>
              </section>
            } @else {
              <div class="actions"><button type="button" (click)="startEdit(item)">Editează datele firmei</button>
                <button type="button" class="danger" (click)="requestDelete()">Șterge firma</button></div>
            }
          }
          <div class="link-row"><a routerLink="/admin/supplier-discovery">Vezi potrivirile</a>
            <a routerLink="/admin/supplier-discovery/acquisition" [queryParams]="{candidateId:item.id}">Vezi în Achiziție furnizori →</a></div>
        }
      } @else {
        <form class="card form" [formGroup]="form" (ngSubmit)="save(item.id)">
          <div><h2>Editează firma descoperită</h2><p>Corectezi informația găsită; sursa și istoricul potrivirilor sunt păstrate.</p></div>
          <div class="form-grid">
            <label class="field full">Denumirea firmei <input formControlName="companyName" autocomplete="organization"> @if (invalid('companyName')) { <small class="error">Completează denumirea firmei.</small> }</label>
            <label class="field">Localitate <input formControlName="locality" autocomplete="address-level2"> @if (invalid('locality')) { <small class="error">Completează localitatea.</small> }</label>
            <label class="field">Județ <input formControlName="county" autocomplete="address-level1"> @if (invalid('county')) { <small class="error">Completează județul.</small> }</label>
            <label class="field">E-mail <input formControlName="email" type="email" autocomplete="email"> @if (invalid('email')) { <small class="error">Introdu o adresă validă sau lasă câmpul gol.</small> }</label>
            <label class="field">Telefon <input formControlName="phone" type="tel" autocomplete="tel"></label>
            <label class="field full">Website <input formControlName="website" type="url" autocomplete="url"></label>
          </div>
          <fieldset><legend>Categorii de produse / servicii</legend><div class="categories">@for (category of categories(); track category.id) { <label><input type="checkbox" [checked]="selectedCategories().has(category.id)" (change)="toggleCategory(category, $any($event.target).checked)"> {{ category.name }}</label> }</div></fieldset>
          @if (saveError()) { <p class="error" role="alert">{{ saveError() }}</p> }
          <div class="actions"><button type="submit" [disabled]="saving()">{{ saving() ? 'Se salvează…' : 'Salvează modificările' }}</button><button type="button" class="secondary" [disabled]="saving()" (click)="cancelEdit()">Renunță</button></div>
        </form>
      }
    } @else { <section class="state">Firma nu este disponibilă.</section> }
  </section>`
})
export class CompanyDetailEditorComponent {
  private readonly registry = inject(CompanyRegistryService);
  private readonly settings = inject(SupplierSettingsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly company = signal<CompanyRegistryEntry | null>(null);
  readonly categories = signal<CategoryOption[]>([]);
  readonly selectedCategories = signal<Set<string>>(new Set());
  readonly loading = signal(true);
  readonly editing = signal(false);
  readonly saving = signal(false);
  readonly deleting = signal(false);
  readonly confirmingDelete = signal(false);
  readonly activeSaving = signal(false);
  readonly saveError = signal('');
  readonly form = new FormGroup({
    companyName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(200)] }),
    locality: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(120)] }),
    county: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(120)] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.email, Validators.maxLength(254)] }),
    phone: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(40)] }),
    website: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] })
  });

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    const kind = this.route.snapshot.paramMap.get('kind');
    this.registry.list().subscribe({ next: items => { this.company.set(items.find(item => item.id === id && item.kind === kind) ?? null); this.loading.set(false); }, error: () => this.loading.set(false) });
    this.settings.getCategories().subscribe({ next: categories => { this.categories.set(categories); const company = this.company(); if (company && this.editing()) this.selectCurrentCategories(company, categories); } });
  }

  startEdit(company: CompanyRegistryEntry): void {
    this.saveError.set('');
    this.form.reset({ companyName: company.name, locality: company.locality, county: company.county, email: company.email ?? '', phone: company.phone ?? '', website: company.website ?? '' });
    this.selectCurrentCategories(company, this.categories());
    this.editing.set(true);
  }

  cancelEdit(): void { this.editing.set(false); this.saveError.set(''); }

  toggleCategory(category: CategoryOption, selected: boolean): void {
    this.selectedCategories.update(current => { const next = new Set(current); selected ? next.add(category.id) : next.delete(category.id); return next; });
  }

  private selectCurrentCategories(company: CompanyRegistryEntry, categories: CategoryOption[]): void {
    this.selectedCategories.set(new Set(categories.filter(category => company.categories.includes(category.name)).map(category => category.id)));
  }

  invalid(controlName: keyof typeof this.form.controls): boolean { const control = this.form.controls[controlName]; return control.invalid && control.touched; }

  save(id: string): void {
    if (this.form.invalid || this.selectedCategories().size === 0) { this.form.markAllAsTouched(); if (this.selectedCategories().size === 0) this.saveError.set('Selectează cel puțin o categorie.'); return; }
    this.saving.set(true); this.saveError.set('');
    const values = this.form.getRawValue();
    this.registry.updateDiscovered(id, { ...values, email: values.email || null, phone: values.phone || null, website: values.website || null, categoryIds: [...this.selectedCategories()] }).subscribe({
      next: company => { this.company.set(company); this.saving.set(false); this.editing.set(false); },
      error: response => { this.saving.set(false); this.saveError.set(response?.error?.errors?.company?.[0] ?? 'Firma nu a putut fi actualizată. Încearcă din nou.'); }
    });
  }

  requestDelete(): void { this.saveError.set(''); this.confirmingDelete.set(true); }

  cancelDelete(): void { this.confirmingDelete.set(false); this.saveError.set(''); }

  confirmDelete(company: CompanyRegistryEntry): void {
    this.deleting.set(true);
    this.saveError.set('');
    this.registry.deleteDiscovered(company.id).subscribe({
      next: () => { this.deleting.set(false); void this.router.navigateByUrl('/admin/companies'); },
      error: response => { this.deleting.set(false); this.confirmingDelete.set(false); this.saveError.set(response?.error?.errors?.company?.[0] ?? 'Firma nu a putut fi ștearsă.'); }
    });
  }

  status(item: CompanyRegistryEntry): string { return item.kind === 'Registered' ? 'Firmă înregistrată' : item.claimed ? 'Firmă descoperită · revendicată' : 'Firmă descoperită · nerevendicată'; }

  statusPill(item: CompanyRegistryEntry): string { return item.kind === 'Registered' ? (item.isActive ? 'registered' : 'inactive') : item.claimed ? 'claimed' : 'discovered'; }

  toggleActive(item: CompanyRegistryEntry): void {
    const activate = !item.isActive;
    if (!confirm(activate
      ? `Activezi firma „${item.name}”? Reapare în potrivirile pentru noi oportunități.`
      : `Dezactivezi firma „${item.name}”? Nu va mai primi potriviri noi, dar ofertele existente rămân valabile.`)) return;
    this.activeSaving.set(true);
    this.saveError.set('');
    this.registry.setRegisteredActive(item.id, activate).subscribe({
      next: company => { if (company) this.company.set(company); this.activeSaving.set(false); },
      error: () => { this.activeSaving.set(false); this.saveError.set('Starea firmei nu a putut fi schimbată. Încearcă din nou.'); }
    });
  }

  kindMark(item: CompanyRegistryEntry): string { return item.kind === 'Registered' ? '✓' : item.claimed ? '♧' : '⌕'; }
}
