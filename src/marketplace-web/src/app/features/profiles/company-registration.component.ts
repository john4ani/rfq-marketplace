import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ProfileService, RepresentedCompany, SupplierProfile } from '../../core/profiles/profile.service';
import { CategoryOption, SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';
import { ROMANIAN_COUNTIES } from '../../shared/romanian-counties';
import { SupplierSettingsComponent } from './supplier-settings.component';

@Component({
  selector: 'app-company-registration',
  imports: [ReactiveFormsModule, RouterLink, SupplierSettingsComponent],
  template: `<section class="registration-shell">
    <a class="back" routerLink="/account">← Înapoi la cont</a>
    <header><p class="eyebrow">Firma ta</p><h1>{{ existingCompany() ? 'Editează firma' : 'Adaugă firma' }}</h1>
      <p>Contul tău personal rămâne același și îl poți folosi în continuare pentru achiziții.</p></header>

    @if (existingCompany()) {
      <div class="single-company-banner" role="status">
        <p><strong>Ai deja înregistrată firma „{{ existingCompany()!.name }}".</strong> Momentan este suportată o singură firmă per cont — poți edita firma existentă sau o poți vizualiza.</p>
        <a routerLink="/account/company/{{ existingCompany()!.id }}">Vezi firma</a>
      </div>
    }

    @if (!createdCompany()) {
      <form class="company-form" [formGroup]="form" (ngSubmit)="createCompany()" novalidate>
        <div class="section-heading"><div><p class="eyebrow">Date esențiale</p><h2>Identitate și contact</h2></div>
          <p>Completează doar informațiile necesare pentru ca firma să poată răspunde oportunităților.</p></div>
        <div class="form-grid">
          <label class="wide">Denumirea firmei *<input formControlName="tradingName" autocomplete="organization" placeholder="Ex: Firma Mea SRL"></label>
          <label>Număr de înregistrare *<input formControlName="companyRegistrationNumber" placeholder="Ex: J33/123/2026"></label>
          <label>Persoană de contact *<input formControlName="contactPerson" autocomplete="name"></label>
          <label>E-mail de contact *<input type="email" formControlName="contactEmail" autocomplete="email"></label>
          <label>Telefon *<input formControlName="phoneNumber" autocomplete="tel" placeholder="+40 7xx xxx xxx"></label>
          <label class="wide">Adresă *<input formControlName="address" autocomplete="street-address"></label>
          <label>Localitate *<input formControlName="city" autocomplete="address-level2" placeholder="Sat, comună, oraș sau municipiu"></label>
          <label>Județ *<select formControlName="county" autocomplete="address-level1"><option value="">Alege județul</option>@for (county of counties; track county) { <option [value]="county">{{ county }}</option> }</select></label>
          <label>Țară *<input formControlName="country" autocomplete="country-name"></label>
          <label>Rază de interes (km) *<input type="number" min="1" max="1000" formControlName="marketRadiusKm"><small>Raza în care firma vrea în mod normal să vadă oportunități.</small></label>
        </div>
        <section class="category-selection" aria-labelledby="company-categories-heading">
          <div class="section-heading"><div><p class="eyebrow">Ce oferă firma</p><h2 id="company-categories-heading">Categorii de produse</h2></div>
            <p>Alege una sau mai multe categorii de produse ori servicii pentru care firma poate răspunde.</p></div>
          @if (loadingCategories()) { <p role="status">Se încarcă categoriile…</p> }
          @else if (categoriesLoadFailed()) { <p class="form-error" role="alert">Categoriile nu sunt disponibile momentan. Reîncarcă pagina și încearcă din nou.</p> }
          @else { <div class="choice-grid" role="group" aria-label="Categorii de produse">
            @for (category of selectableCategories(); track category.id) {
              <label class="choice-card" [class.selected]="selectedCategoryIds().includes(category.id)">
                <input type="checkbox" [checked]="selectedCategoryIds().includes(category.id)" (change)="toggleCategory(category.id)">
                <span>{{ category.name }}</span>
              </label>
            }
          </div> }
          <p class="field-help">Poți alege mai multe categorii. Ele ajută CompaSpot să arate firmei doar oportunități relevante.</p>
          @if (!loadingCategories() && !categoriesLoadFailed() && selectedCategoryIds().length === 0) { <p class="field-error">Alege cel puțin o categorie de produse sau servicii.</p> }
        </section>
        <section class="category-selection" aria-labelledby="company-areas-heading">
          <div class="section-heading"><div><p class="eyebrow">Unde activează firma</p><h2 id="company-areas-heading">Județe deservite</h2></div>
            <p>Alege toate județele în care firma poate răspunde cererilor.</p></div>
          <label class="nationwide-choice"><input type="checkbox" [checked]="nationwideDelivery()" (change)="toggleNationwideDelivery()"><span><strong>Activăm firma în toată România</strong><small>Firma va putea vedea oportunități din orice județ.</small></span></label>
          @if (!nationwideDelivery()) { <div class="county-grid" role="group" aria-label="Județe deservite">
            @for (county of counties; track county) {
              <label class="choice-card" [class.selected]="selectedCounties().includes(county)">
                <input type="checkbox" [checked]="selectedCounties().includes(county)" (change)="toggleCounty(county)">
                <span>{{ county }}</span>
              </label>
            }
          </div> }
          <p class="field-help">Poți selecta mai multe județe. Localitatea sediului nu limitează zona în care firma activează.</p>
          @if (!nationwideDelivery() && selectedCounties().length === 0) { <p class="field-error">Alege cel puțin un județ sau activarea în toată România.</p> }
        </section>
        <details><summary>Informații opționale</summary><div class="form-grid optional-fields">
          <label>Cod fiscal<input formControlName="taxIdentifier"></label>
          <label>Website<input formControlName="website" placeholder="https://firma.ro"></label>
          <label class="wide">Descriere scurtă<textarea rows="3" formControlName="description" placeholder="Ce produse sau servicii oferă firma?"></textarea></label>
        </div></details>
        @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
        <div class="form-actions"><button type="submit" [disabled]="form.invalid || saving() || loadingCategories() || categoriesLoadFailed() || selectedCategoryIds().length === 0 || (!nationwideDelivery() && selectedCounties().length === 0)">{{ saving() ? 'Se adaugă…' : 'Adaugă firma' }}</button></div>
      </form>
    } @else {
      <section class="created-state" role="status"><span aria-hidden="true">✓</span><div><strong>{{ createdCompany()!.tradingName }} a fost adăugată.</strong>
        <p>Ești proprietarul firmei în CompaSpot. Completează ce oferă și unde lucrează pentru potriviri relevante.</p></div></section>
      @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
      <section class="setup-progress" aria-label="Progres configurare firmă"><div class="complete"><span>✓</span><strong>Firma și contactul</strong></div><div [class.complete]="categoriesConfigured()"><span>{{ categoriesConfigured() ? '✓' : '2' }}</span><strong>Produse și servicii</strong></div><div [class.complete]="deliveryConfigured()"><span>{{ deliveryConfigured() ? '✓' : '3' }}</span><strong>Zonă de lucru</strong></div></section>
      <app-supplier-settings [companyId]="createdCompany()!.id" (categoriesSaved)="categoriesConfigured.set(true)" (deliverySaved)="deliveryConfigured.set(true)" />
      <section class="continue-card"><div><strong>{{ matchingReady() ? 'Firma este pregătită pentru potriviri.' : 'Poți continua și completa setările mai târziu.' }}</strong>
        <p>Oportunitățile relevante depind de categoriile și zona salvate.</p></div>@if(returnUrl){<a [routerLink]="returnUrl">Revino la invitație →</a>}@else{<a [routerLink]="'/opportunities'" [queryParams]="{ companyId: createdCompany()!.id }">Vezi oportunitățile →</a>}</section>
    }
  </section>`,
  styleUrl: './company-registration.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CompanyRegistrationComponent {
  private readonly auth = inject(AuthService);
  private readonly profiles = inject(ProfileService);
  private readonly supplierSettings = inject(SupplierSettingsService);
  readonly returnUrl = inject(ActivatedRoute).snapshot.queryParamMap.get('returnUrl')?.startsWith('/supplier-invitations/') ? inject(ActivatedRoute).snapshot.queryParamMap.get('returnUrl') : null;
  private readonly prefillCompanyName = inject(ActivatedRoute).snapshot.queryParamMap.get('companyName');
  readonly counties = ROMANIAN_COUNTIES;
  readonly saving = signal(false);
  readonly error = signal('');
  readonly createdCompany = signal<SupplierProfile | null>(null);
  readonly existingCompany = signal<RepresentedCompany | null>(null);
  readonly companiesLoading = signal(false);
  readonly categoriesConfigured = signal(false);
  readonly deliveryConfigured = signal(false);
  readonly matchingReady = computed(() => this.categoriesConfigured() && this.deliveryConfigured());
  readonly categories = signal<CategoryOption[]>([]);
  readonly selectedCategoryIds = signal<string[]>([]);
  readonly selectedCounties = signal<string[]>([]);
  readonly nationwideDelivery = signal(false);
  readonly loadingCategories = signal(true);
  readonly categoriesLoadFailed = signal(false);
  readonly selectableCategories = computed(() => {
    const categories = this.categories();
    const parentIds = new Set(categories.flatMap(category => category.parentId ? [category.parentId] : []));
    return categories.filter(category => !parentIds.has(category.id));
  });
  readonly form = new FormGroup({
    tradingName: new FormControl(this.prefillCompanyName ?? '', { nonNullable: true, validators: Validators.required }),
    companyRegistrationNumber: new FormControl('', { nonNullable: true, validators: Validators.required }),
    taxIdentifier: new FormControl<string | null>(null),
    contactPerson: new FormControl('', { nonNullable: true, validators: Validators.required }),
    contactEmail: new FormControl(this.auth.identity()?.email ?? '', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    phoneNumber: new FormControl('', { nonNullable: true, validators: Validators.required }),
    website: new FormControl<string | null>(null, { validators: Validators.pattern(/^https?:\/\/.+/i) }),
    address: new FormControl('', { nonNullable: true, validators: Validators.required }),
    city: new FormControl('', { nonNullable: true, validators: Validators.required }),
    county: new FormControl('', { nonNullable: true, validators: Validators.required }),
    country: new FormControl('România', { nonNullable: true, validators: Validators.required }),
    marketRadiusKm: new FormControl(30, { nonNullable: true, validators: [Validators.required, Validators.min(1), Validators.max(1000)] }),
    description: new FormControl<string | null>(null)
  });

  constructor() {
    this.loadCategories();
    this.loadExistingCompany();
  }

  private loadExistingCompany(): void {
    this.companiesLoading.set(true);
    this.profiles.getMyCompanies().subscribe({
      next: values => { this.existingCompany.set(values.length > 0 ? values[0] : null); this.companiesLoading.set(false); },
      error: () => this.companiesLoading.set(false)
    });
  }

  toggleCategory(categoryId: string): void {
    const selected = this.selectedCategoryIds();
    this.selectedCategoryIds.set(selected.includes(categoryId)
      ? selected.filter(id => id !== categoryId)
      : [...selected, categoryId]);
  }

  toggleCounty(county: string): void {
    const selected = this.selectedCounties();
    this.selectedCounties.set(selected.includes(county)
      ? selected.filter(value => value !== county)
      : [...selected, county]);
  }

  toggleNationwideDelivery(): void {
    this.nationwideDelivery.update(value => !value);
  }

  createCompany(): void {
    const userId = this.auth.identity()?.userId;
    if (!userId || this.form.invalid || this.saving()) return;
    this.saving.set(true); this.error.set('');
    this.profiles.saveSupplier(userId, this.form.getRawValue()).subscribe({
      next: company => this.saveInitialCategories(company),
      error: error => { this.saving.set(false); this.error.set(this.apiError(error)); }
    });
  }

  private loadCategories(): void {
    this.loadingCategories.set(true);
    this.categoriesLoadFailed.set(false);
    this.supplierSettings.getCategories().subscribe({
      next: categories => { this.categories.set(categories); this.loadingCategories.set(false); },
      error: () => { this.loadingCategories.set(false); this.categoriesLoadFailed.set(true); }
    });
  }

  private saveInitialCategories(company: SupplierProfile): void {
    this.supplierSettings.saveSupplierCategories(company.id, this.selectedCategoryIds()).subscribe({
      next: () => this.saveInitialDeliveryArea(company),
      error: error => {
        this.createdCompany.set(company);
        this.saving.set(false);
        this.error.set(`Firma a fost adăugată, dar categoriile nu au putut fi salvate. ${this.apiError(error)}`);
      }
    });
  }

  private saveInitialDeliveryArea(company: SupplierProfile): void {
    this.supplierSettings.saveDeliverySettings(company.id, {
      normalDeliveryRadiusKm: 30,
      maximumDeliveryRadiusKm: 30,
      minimumOrderValue: null,
      nationwideDelivery: this.nationwideDelivery(),
      counties: this.nationwideDelivery() ? [] : this.selectedCounties()
    }).subscribe({
      next: () => {
        this.createdCompany.set(company);
        this.categoriesConfigured.set(true);
        this.deliveryConfigured.set(true);
        this.saving.set(false);
      },
      error: error => {
        this.createdCompany.set(company);
        this.categoriesConfigured.set(true);
        this.saving.set(false);
        this.error.set(`Firma și categoriile au fost salvate, dar zona de activitate nu a putut fi salvată. ${this.apiError(error)}`);
      }
    });
  }

  private apiError(error: HttpErrorResponse): string {
    const validation = error.error?.errors as Record<string, string[]> | undefined;
    const firstValidation = validation ? Object.values(validation).flat()[0] : null;
    if (firstValidation) return firstValidation;
    if (error.status === 409) return 'Firma există deja sau este asociată unui alt flux. Verifică firmele din Cont.';
    return error.error?.detail ?? 'Firma nu a putut fi adăugată. Verifică datele și încearcă din nou.';
  }
}
