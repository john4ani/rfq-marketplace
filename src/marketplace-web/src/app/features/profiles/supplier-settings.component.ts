import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { ProfileService, SupplierProfile } from '../../core/profiles/profile.service';
import { FeaturesService } from '../../core/features/features.service';
import { SeapService } from '../../core/seap/seap.service';

import { CategoryOption, SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';
import { ROMANIAN_COUNTIES } from '../../shared/romanian-counties';
import { normalizeCounty, normalizeCpvPrefix } from '../../shared/seap-filters';

@Component({
  selector: 'app-supplier-settings',
  imports: [ReactiveFormsModule],
  template: `@if (companyId() || auth.session()?.accountType === 'Supplier') {
    @if (features.marketplaceEnabled()) {
    <section class="profile-card settings-card">
      <div class="section-heading"><div><p class="eyebrow">Ce livrezi</p><h2>Categorii de produse</h2></div>
        <p>Alege produsele și serviciile pentru care vrei să primești cereri de ofertă.</p></div>
      @if (loadingCategories()) { <p role="status">Se încarcă categoriile…</p> }
      @else if (categoryLoadFailed()) { <div class="state-error" role="alert"><p>Categoriile nu sunt disponibile momentan.</p><button type="button" class="secondary" (click)="loadCategories()">Încearcă din nou</button></div> }
      @else { <div class="choice-grid" role="group" aria-label="Categorii de produse">
        @for (category of selectableCategories(); track category.id) {
          <label class="choice-card" [class.selected]="selectedCategoryIds().includes(category.id)">
            <input type="checkbox" [checked]="selectedCategoryIds().includes(category.id)" (change)="toggleCategory(category.id)">
            <span>{{ category.name }}</span>
          </label>
        }
      </div> }
      <p class="field-help">Nu găsești o categorie potrivită? Trimite administratorului denumirea pieței lipsă; catalogul este comun tuturor participanților.</p>
      <div class="form-actions"><button type="button" (click)="saveCategories()" [disabled]="categoryLoadFailed() || selectedCategoryIds().length === 0 || savingCategories()">
        {{ savingCategories() ? 'Se salvează…' : 'Salvează categoriile' }}
      </button></div>
      @if (categoryMessage()) { <p [class.error]="categoryError()" role="status">{{ categoryMessage() }}</p> }
    </section>
    }

    @if (companyId()) {
    <section class="profile-card settings-card">
      <div class="section-heading"><div><p class="eyebrow">Anunțuri SEAP</p><h2>Nișa ta SEAP</h2></div>
        <p>Alege domeniile CPV și județele pentru anunțurile publice care te interesează. Salvate pe profilul firmei și folosite ca filtru implicit pe pagina Anunțuri SEAP.</p></div>
      <div class="seap-field"><label>Coduri CPV</label>
        <div class="chips">
          @for (prefix of seapCpvPrefixes(); track prefix) {
            <span class="chip">{{ prefix }}<button type="button" class="chip-remove" (click)="removeSeapCpvPrefix(prefix)" [attr.aria-label]="'Șterge ' + prefix">×</button></span>
          }
          @if (seapCpvPrefixes().length === 0) { <span class="chip-empty">Niciun prefix — toate domeniile</span> }
        </div>
        <div class="chip-input"><input [value]="seapCpvInput()" (input)="seapCpvInput.set($any($event.target).value)" placeholder="ex. 45233000" aria-label="Adaugă prefix CPV">
          <button type="button" (click)="addSeapCpvPrefix()" [disabled]="!normalizeCpvPrefix(seapCpvInput())">Adaugă</button></div>
      </div>
      <div class="seap-field"><label>Județe</label>
        <div class="chips">
          @for (county of seapCounties(); track county) {
            <span class="chip">{{ county }}<button type="button" class="chip-remove" (click)="removeSeapCounty(county)" [attr.aria-label]="'Șterge ' + county">×</button></span>
          }
          @if (seapCounties().length === 0) { <span class="chip-empty">Toată țara</span> }
        </div>
        <div class="chip-input"><select [value]="seapCountyDraft()" (change)="seapCountyDraft.set($any($event.target).value)" aria-label="Alege județul de adăugat">
            <option value="">Alege județul</option>@for (county of availableSeapCounties(); track county) { <option [value]="county">{{ county }}</option> }</select>
          <button type="button" (click)="addSeapCounty()" [disabled]="!seapCountyDraft()">Adaugă</button></div>
      </div>
      <div class="form-actions"><button type="button" (click)="saveSeapProfile()" [disabled]="seapSaving()">
        {{ seapSaving() ? 'Se salvează…' : 'Salvează nișa SEAP' }}
      </button></div>
      @if (seapMessage()) { <p [class.error]="seapError()" role="status">{{ seapMessage() }}</p> }
    </section>
    }

    @if (features.marketplaceEnabled()) {
    <section class="profile-card settings-card">
      <div class="section-heading"><div><p class="eyebrow">Unde livrezi</p><h2>Zonă și rază de livrare</h2></div>
        <p>Raza normală descrie zona ta obișnuită de livrare; nu este un filtru de vizibilitate sau clasare.</p></div>
      <form [formGroup]="deliveryForm" (ngSubmit)="saveDelivery()">
        <div class="form-row">
          <label>Rază normală (km) *<input type="number" min="1" max="1000" formControlName="normalDeliveryRadiusKm">
            <small>Distanța în care livrezi în mod obișnuit.</small></label>
          <label>Rază maximă (km) *<input type="number" min="1" max="1000" formControlName="maximumDeliveryRadiusKm">
            <small>Limita până la care poți accepta o comandă.</small></label>
        </div>
        <label>Valoare minimă a comenzii (lei)<input type="number" min="0" formControlName="minimumOrderValue">
          <small>Opțional. Lasă câmpul gol dacă nu ai o limită.</small></label>
        <label class="checkbox-label"><input type="checkbox" formControlName="nationwideDelivery">Livrez oriunde în România</label>
        @if (!deliveryForm.controls.nationwideDelivery.value) {
          <fieldset><legend>Județe deservite *</legend><p class="field-help">Alege toate județele în care livrezi în mod normal.</p>
            <div class="county-grid">
              @for (county of counties; track county) {
                <label class="county-choice" [class.selected]="selectedCounties().includes(county)">
                  <input type="checkbox" [checked]="selectedCounties().includes(county)" (change)="toggleCounty(county)">{{ county }}
                </label>
              }
            </div>
          </fieldset>
        }
        <div class="form-actions"><button [disabled]="deliveryForm.invalid || savingDelivery()">
          {{ savingDelivery() ? 'Se salvează…' : 'Salvează zona de livrare' }}
        </button></div>
      </form>
      @if (deliveryMessage()) { <p [class.error]="deliveryError()" role="status">{{ deliveryMessage() }}</p> }
    </section>
    }

    <section class="profile-card settings-card">
      <div class="section-heading"><div><p class="eyebrow">Anunțuri SEAP</p><h2>Rază de interes pentru anunțuri</h2></div>
        <p>Raza din profilul firmei controlează filtrul „Doar anunțurile în raza de X km de județ" din pagina Anunțuri SEAP.</p></div>
      <form [formGroup]="interestRadiusForm" (ngSubmit)="saveInterestRadius()">
        <label>Rază de interes (km) *<input type="number" min="1" max="1000" formControlName="marketRadiusKm">
          <small>Distanța față de județul firmei în care vrei să vezi anunțuri SEAP.</small></label>
        <div class="form-actions"><button [disabled]="interestRadiusForm.invalid || savingInterestRadius()">
          {{ savingInterestRadius() ? 'Se salvează…' : 'Salvează raza de interes' }}
        </button></div>
      </form>
      @if (interestRadiusMessage()) { <p [class.error]="interestRadiusError()" role="status">{{ interestRadiusMessage() }}</p> }
    </section>

  }`,
  styleUrl: './supplier-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SupplierSettingsComponent {
  readonly companyId = input('');
  readonly categoriesSaved = output<void>();
  readonly deliverySaved = output<void>();
  private readonly profiles = inject(ProfileService);

  readonly auth = inject(AuthService);
  private readonly settings = inject(SupplierSettingsService);
  readonly features = inject(FeaturesService);
  private readonly seap = inject(SeapService);
  readonly counties = ROMANIAN_COUNTIES;
  readonly seapCpvPrefixes = signal<string[]>([]);
  readonly seapCounties = signal<string[]>([]);
  readonly seapCpvInput = signal('');
  readonly seapCountyDraft = signal('');
  readonly seapSaving = signal(false);
  readonly seapMessage = signal('');
  readonly seapError = signal(false);
  readonly normalizeCpvPrefix = normalizeCpvPrefix;
  readonly availableSeapCounties = computed(() => this.counties.filter(county => !this.seapCounties().includes(county)));
  readonly categories = signal<CategoryOption[]>([]);
  readonly selectedCategoryIds = signal<string[]>([]);
  readonly selectedCounties = signal<string[]>([]);
  readonly loadingCategories = signal(true);
  readonly categoryLoadFailed = signal(false);
  readonly savingCategories = signal(false);
  readonly savingDelivery = signal(false);
  readonly categoryMessage = signal('');
  readonly deliveryMessage = signal('');
  readonly categoryError = signal(false);
  readonly deliveryError = signal(false);
  readonly selectableCategories = computed(() => {
    const values = this.categories();
    const parentIds = new Set(values.flatMap(category => category.parentId ? [category.parentId] : []));
    return values.filter(category => !parentIds.has(category.id));
  });
  readonly deliveryForm = new FormGroup({
    normalDeliveryRadiusKm: new FormControl(30, { nonNullable: true, validators: [Validators.required, Validators.min(1), Validators.max(1000)] }),
    maximumDeliveryRadiusKm: new FormControl(30, { nonNullable: true, validators: [Validators.required, Validators.min(1), Validators.max(1000)] }),
    minimumOrderValue: new FormControl<number|null>(null, { validators: Validators.min(0) }),
    nationwideDelivery: new FormControl(false, { nonNullable: true })
  });
  readonly savingInterestRadius = signal(false);
  readonly interestRadiusMessage = signal('');
  readonly interestRadiusError = signal(false);
  readonly interestRadiusForm = new FormGroup({
    marketRadiusKm: new FormControl(30, { nonNullable: true, validators: [Validators.required, Validators.min(1), Validators.max(1000)] })
  });


  constructor() {
    this.loadCategories();
    effect(() => {
      const id = this.companyId() || this.auth.session()?.userId;
      if (!id) return;
      this.loadSupplierSettings(id);
    });
  }

  loadCategories(): void {
    this.loadingCategories.set(true); this.categoryLoadFailed.set(false); this.categoryMessage.set('');
    this.settings.getCategories().subscribe({
      next: categories => { this.categories.set(categories); this.loadingCategories.set(false); },
      error: () => { this.loadingCategories.set(false); this.categoryLoadFailed.set(true); this.categoryError.set(true); this.categoryMessage.set('Categoriile nu au putut fi încărcate.'); }
    });
  }

  toggleCategory(id: string): void {
    const values = this.selectedCategoryIds();
    this.selectedCategoryIds.set(values.includes(id) ? values.filter(value => value !== id) : [...values, id]);
  }
  toggleCounty(county: string): void {
    const values = this.selectedCounties();
    this.selectedCounties.set(values.includes(county) ? values.filter(value => value !== county) : [...values, county]);
  }
  addSeapCpvPrefix(): void {
    const value = this.normalizeCpvPrefix(this.seapCpvInput());
    if (!value) return;
    this.seapCpvPrefixes.update(prefixes => prefixes.includes(value) ? prefixes : [...prefixes, value]);
    this.seapCpvInput.set('');
    this.seapMessage.set(`Prefixul CPV ${value} a fost adăugat la nișa SEAP.`);
    this.seapError.set(false);
  }
  removeSeapCpvPrefix(prefix: string): void {
    this.seapCpvPrefixes.update(prefixes => prefixes.filter(item => item !== prefix));
  }
  addSeapCounty(): void {
    const value = normalizeCounty(this.seapCountyDraft());
    if (!value) return;
    this.seapCounties.update(counties => counties.includes(value) ? counties : [...counties, value]);
    this.seapCountyDraft.set('');
    this.seapMessage.set(`Județul „${value}” a fost adăugat la nișa SEAP.`);
    this.seapError.set(false);
  }
  removeSeapCounty(county: string): void {
    this.seapCounties.update(counties => counties.filter(item => item !== county));
  }
  saveSeapProfile(): void {
    const id = this.companyId() || this.auth.session()?.userId;
    if (!id || this.seapSaving()) return;
    this.seapSaving.set(true); this.seapMessage.set(''); this.seapError.set(false);
    this.seap.saveMatchProfile(id, this.seapCpvPrefixes(), this.seapCounties()).subscribe({
      next: saved => {
        this.seapCpvPrefixes.set(saved.cpvPrefixes);
        this.seapCounties.set(saved.counties);
        this.seapSaving.set(false);
        this.seapMessage.set('Nișa SEAP a fost salvată pe profilul firmei.');
      },
      error: () => {
        this.seapSaving.set(false);
        this.seapError.set(true);
        this.seapMessage.set('Nișa SEAP nu a putut fi salvată.');
      }
    });
  }
  saveCategories(): void {
    const id = this.companyId() || this.auth.session()?.userId;
    if (!id || this.selectedCategoryIds().length === 0) return;
    this.savingCategories.set(true); this.categoryMessage.set('');
    this.settings.saveSupplierCategories(id, this.selectedCategoryIds()).subscribe({
      next: result => { this.selectedCategoryIds.set(result.categoryIds); this.savingCategories.set(false); this.categoryError.set(false); this.categoryMessage.set('Categoriile au fost salvate.'); this.categoriesSaved.emit(); },
      error: error => { this.savingCategories.set(false); this.categoryError.set(true); this.categoryMessage.set(error.status === 404 ? 'Completează și salvează mai întâi profilul companiei.' : 'Categoriile nu au putut fi salvate. Verifică selecția și încearcă din nou.'); }
    });
  }
  saveDelivery(): void {
    const id = this.companyId() || this.auth.session()?.userId;
    const form = this.deliveryForm.getRawValue();
    if (!id || this.deliveryForm.invalid) return;
    if (form.maximumDeliveryRadiusKm < form.normalDeliveryRadiusKm) {
      this.deliveryError.set(true); this.deliveryMessage.set('Raza maximă trebuie să fie cel puțin egală cu raza normală.'); return;
    }
    if (!form.nationwideDelivery && this.selectedCounties().length === 0) {
      this.deliveryError.set(true); this.deliveryMessage.set('Alege cel puțin un județ sau livrarea în toată România.'); return;
    }
    this.savingDelivery.set(true); this.deliveryMessage.set('');
    this.settings.saveDeliverySettings(id, { ...form, counties: form.nationwideDelivery ? [] : this.selectedCounties() }).subscribe({
      next: result => { this.deliveryForm.patchValue(result); this.selectedCounties.set(result.counties); this.savingDelivery.set(false); this.deliveryError.set(false); this.deliveryMessage.set('Zona de livrare a fost salvată.'); this.deliverySaved.emit(); },
      error: error => { this.savingDelivery.set(false); this.deliveryError.set(true); this.deliveryMessage.set(error.status === 404 ? 'Completează și salvează mai întâi profilul companiei.' : 'Zona de livrare nu a putut fi salvată. Verifică razele și județele selectate.'); }
    });
  }

  private loadSupplierSettings(id: string): void {
    this.settings.getSupplierCategories(id).subscribe({
      next: result => this.selectedCategoryIds.set(result.categoryIds),
      error: error => { if (error.status !== 404) { this.categoryError.set(true); this.categoryMessage.set('Selecția de categorii nu a putut fi încărcată.'); } }
    });
    this.settings.getDeliverySettings(id).subscribe({
      next: result => { this.deliveryForm.patchValue(result); this.selectedCounties.set(result.counties); },
      error: error => { if (error.status !== 404) { this.deliveryError.set(true); this.deliveryMessage.set('Setările de livrare nu au putut fi încărcate.'); } }
    });
    this.profiles.getSupplier(id).subscribe({
      next: profile => {
        this.seapCpvPrefixes.set(profile.seapCpvPrefixes ?? []);
        this.seapCounties.set(profile.seapCounties ?? []);
        this.interestRadiusForm.patchValue({ marketRadiusKm: profile.marketRadiusKm });
      },
      error: () => { /* raza rămâne la valoarea implicită dacă profilul nu e disponibil */ }
    });
  }

  saveInterestRadius(): void {
    const id = this.companyId() || this.auth.session()?.userId;
    if (!id || this.interestRadiusForm.invalid) return;
    this.savingInterestRadius.set(true); this.interestRadiusMessage.set('');
    this.profiles.getSupplier(id).subscribe({
      next: profile => {
        this.profiles.saveSupplier(id, { ...profile, marketRadiusKm: this.interestRadiusForm.getRawValue().marketRadiusKm }).subscribe({
          next: saved => { this.interestRadiusForm.patchValue({ marketRadiusKm: saved.marketRadiusKm }); this.savingInterestRadius.set(false); this.interestRadiusError.set(false); this.interestRadiusMessage.set('Raza de interes a fost salvată. Anunțurile SEAP o folosesc imediat.'); },
          error: error => { this.savingInterestRadius.set(false); this.interestRadiusError.set(true); this.interestRadiusMessage.set(error.status === 404 ? 'Completează și salvează mai întâi profilul companiei.' : 'Raza de interes nu a putut fi salvată.'); }
        });
      },
      error: error => { this.savingInterestRadius.set(false); this.interestRadiusError.set(true); this.interestRadiusMessage.set(error.status === 404 ? 'Completează și salvează mai întâi profilul companiei.' : 'Profilul companiei nu a putut fi încărcat.'); }
    });
  }



}
