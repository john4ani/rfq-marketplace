import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { CategoryOption, SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';
import { ROMANIAN_COUNTIES } from '../../shared/romanian-counties';

@Component({
  selector: 'app-supplier-settings',
  imports: [ReactiveFormsModule],
  template: `@if (auth.session()?.accountType === 'Supplier') {
    <section class="profile-card settings-card">
      <div class="section-heading"><div><p class="eyebrow">Ce livrezi</p><h2>Categorii de produse</h2></div>
        <p>Alege materialele pentru care vrei să primești cereri de ofertă.</p></div>
      @if (loadingCategories()) { <p role="status">Se încarcă categoriile…</p> }
      <div class="choice-grid" role="group" aria-label="Categorii de produse">
        @for (category of selectableCategories(); track category.id) {
          <label class="choice-card" [class.selected]="selectedCategoryIds().includes(category.id)">
            <input type="checkbox" [checked]="selectedCategoryIds().includes(category.id)" (change)="toggleCategory(category.id)">
            <span>{{ category.name }}</span>
          </label>
        }
      </div>
      <p class="field-help">Nu găsești o categorie potrivită? Trimite administratorului denumirea pieței lipsă; catalogul este comun tuturor participanților.</p>
      <div class="form-actions"><button type="button" (click)="saveCategories()" [disabled]="selectedCategoryIds().length === 0 || savingCategories()">
        {{ savingCategories() ? 'Se salvează…' : 'Salvează categoriile' }}
      </button></div>
      @if (categoryMessage()) { <p [class.error]="categoryError()" role="status">{{ categoryMessage() }}</p> }
    </section>

    <section class="profile-card settings-card">
      <div class="section-heading"><div><p class="eyebrow">Unde livrezi</p><h2>Zonă și rază de livrare</h2></div>
        <p>Aceste date vor fi folosite ulterior pentru a afișa doar cererile relevante.</p></div>
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
  }`,
  styleUrl: './supplier-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SupplierSettingsComponent {
  readonly auth = inject(AuthService);
  private readonly settings = inject(SupplierSettingsService);
  readonly counties = ROMANIAN_COUNTIES;
  readonly categories = signal<CategoryOption[]>([]);
  readonly selectedCategoryIds = signal<string[]>([]);
  readonly selectedCounties = signal<string[]>([]);
  readonly loadingCategories = signal(true);
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
    normalDeliveryRadiusKm: new FormControl(50, { nonNullable: true, validators: [Validators.required, Validators.min(1), Validators.max(1000)] }),
    maximumDeliveryRadiusKm: new FormControl(100, { nonNullable: true, validators: [Validators.required, Validators.min(1), Validators.max(1000)] }),
    minimumOrderValue: new FormControl<number|null>(null, { validators: Validators.min(0) }),
    nationwideDelivery: new FormControl(false, { nonNullable: true })
  });

  constructor() {
    this.settings.getCategories().subscribe({
      next: categories => { this.categories.set(categories); this.loadingCategories.set(false); },
      error: () => { this.loadingCategories.set(false); this.categoryError.set(true); this.categoryMessage.set('Categoriile nu au putut fi încărcate. Verifică dacă API-ul rulează.'); }
    });
    effect(() => {
      const session = this.auth.session();
      if (!session || session.accountType !== 'Supplier') return;
      this.loadSupplierSettings(session.userId);
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
  saveCategories(): void {
    const session = this.auth.session();
    if (!session || this.selectedCategoryIds().length === 0) return;
    this.savingCategories.set(true); this.categoryMessage.set('');
    this.settings.saveSupplierCategories(session.userId, this.selectedCategoryIds()).subscribe({
      next: result => { this.selectedCategoryIds.set(result.categoryIds); this.savingCategories.set(false); this.categoryError.set(false); this.categoryMessage.set('Categoriile au fost salvate.'); },
      error: error => { this.savingCategories.set(false); this.categoryError.set(true); this.categoryMessage.set(error.status === 404 ? 'Completează și salvează mai întâi profilul companiei.' : 'Categoriile nu au putut fi salvate. Verifică selecția și încearcă din nou.'); }
    });
  }
  saveDelivery(): void {
    const session = this.auth.session();
    const form = this.deliveryForm.getRawValue();
    if (!session || this.deliveryForm.invalid) return;
    if (form.maximumDeliveryRadiusKm < form.normalDeliveryRadiusKm) {
      this.deliveryError.set(true); this.deliveryMessage.set('Raza maximă trebuie să fie cel puțin egală cu raza normală.'); return;
    }
    if (!form.nationwideDelivery && this.selectedCounties().length === 0) {
      this.deliveryError.set(true); this.deliveryMessage.set('Alege cel puțin un județ sau livrarea în toată România.'); return;
    }
    this.savingDelivery.set(true); this.deliveryMessage.set('');
    this.settings.saveDeliverySettings(session.userId, { ...form, counties: form.nationwideDelivery ? [] : this.selectedCounties() }).subscribe({
      next: result => { this.deliveryForm.patchValue(result); this.selectedCounties.set(result.counties); this.savingDelivery.set(false); this.deliveryError.set(false); this.deliveryMessage.set('Zona de livrare a fost salvată.'); },
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
  }

}
