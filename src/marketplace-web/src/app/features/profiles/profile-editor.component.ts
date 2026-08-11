import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, effect, inject, output, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { ProfileService } from '../../core/profiles/profile.service';
import { ROMANIAN_COUNTIES } from '../../shared/romanian-counties';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-profile-editor', imports: [ReactiveFormsModule],
  template: `<section class="profile-card"><div class="section-heading"><div><p class="eyebrow">Datele tale</p><h2>Profil {{ auth.session()?.accountType === 'Buyer' ? 'cumpărător' : 'furnizor' }}</h2></div><p>Completează doar informațiile necesare. Le poți modifica oricând.</p></div>
  @if (loading()) { <p role="status">Se încarcă profilul…</p> }
  @else if (loadFailed()) { <div class="profile-load-error" role="alert"><p>Profilul nu a putut fi încărcat. Nu îl poți edita în siguranță până nu avem datele actuale.</p><button type="button" (click)="loadProfile()">Încearcă din nou</button></div> }
  @else if (auth.session()?.accountType === 'Buyer') {
    <form [formGroup]="buyerForm" (ngSubmit)="saveBuyer()">
      <fieldset><legend>Informații de bază</legend>
      <label>Nume afișat *<input formControlName="displayName" placeholder="Cum să te afișăm?"></label>
      <label>Tip cumpărător *<select formControlName="buyerType"><option value="Individual">Persoană fizică</option><option value="Company">Companie</option></select></label>
      @if (buyerForm.controls.buyerType.value === 'Company') {
        <label>Numele companiei<input formControlName="companyName"></label>
        <label>CUI/CIF<input formControlName="taxIdentifier"></label>
      }</fieldset>
      <fieldset><legend>Contact și locație</legend>
      <label>Telefon *<input formControlName="phoneNumber" placeholder="+40 7xx xxx xxx"></label>
      <div class="form-row"><label>Oraș *<input formControlName="defaultCity"></label>
      <label>Județ *<select formControlName="defaultCounty"><option value="">Alege județul</option>@for (county of counties; track county) { <option [value]="county">{{ county }}</option> }</select></label></div>
      <label>Rază de interes (km) *<input type="number" min="1" max="1000" formControlName="marketRadiusKm">
        <small>Valoarea implicită este 30 km. În prima versiune, Piața Live afișează orașul tău și alte localități din același județ.</small></label>
      </fieldset>
      <div class="form-actions"><button [disabled]="buyerForm.invalid || saving()">{{ saving() ? 'Se salvează…' : 'Salvează profilul' }}</button></div>
    </form>
  } @else {
    <form [formGroup]="supplierForm" (ngSubmit)="saveSupplier()">
      <fieldset><legend>Compania</legend>
      <label>Denumire comercială *<input formControlName="tradingName"></label>
      <div class="form-row"><label>Nr. Registrul Comerțului *<input formControlName="companyRegistrationNumber" placeholder="J00/000/2026"></label>
      <label>CUI/CIF<input formControlName="taxIdentifier"></label>
      </div><label>Descriere scurtă<textarea formControlName="description" rows="3" placeholder="Ce materiale furnizați și în ce zonă?"></textarea></label></fieldset>
      <fieldset><legend>Persoană de contact</legend>
      <label>Nume *<input formControlName="contactPerson"></label>
      <div class="form-row"><label>E-mail *<input type="email" formControlName="contactEmail"></label>
      <label>Telefon *<input formControlName="phoneNumber" placeholder="+40 7xx xxx xxx"></label></div>
      <label>Website<input type="url" formControlName="website" placeholder="https://exemplu.ro"></label></fieldset>
      <fieldset><legend>Sediu</legend><label>Adresă *<input formControlName="address"></label>
      <div class="form-row"><label>Oraș *<input formControlName="city"></label>
      <label>Județ *<select formControlName="county"><option value="">Alege județul</option>@for (county of counties; track county) { <option [value]="county">{{ county }}</option> }</select></label></div></fieldset>
      <div class="form-actions"><button [disabled]="supplierForm.invalid || saving()">{{ saving() ? 'Se salvează…' : 'Salvează profilul' }}</button></div>
    </form>
  }
  @if (message()) { <p [class.error]="hasError()" role="status">{{ message() }}</p> }
  </section>`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProfileEditorComponent {
  readonly auth = inject(AuthService); private readonly profiles = inject(ProfileService);
  readonly profileSaved = output<'Buyer' | 'Supplier'>();
  readonly counties = ROMANIAN_COUNTIES; readonly loading = signal(false); readonly saving = signal(false); readonly message = signal(''); readonly hasError = signal(false); readonly loadFailed = signal(false);
  readonly buyerForm = new FormGroup({
    displayName: new FormControl('', { nonNullable: true, validators: Validators.required }),
    buyerType: new FormControl<'Individual'|'Company'>('Individual', { nonNullable: true }),
    companyName: new FormControl<string|null>(null), taxIdentifier: new FormControl<string|null>(null),
    phoneNumber: new FormControl('', { nonNullable: true, validators: Validators.required }),
    defaultCity: new FormControl('', { nonNullable: true, validators: Validators.required }),
    defaultCounty: new FormControl('', { nonNullable: true, validators: Validators.required }),
    marketRadiusKm: new FormControl(30, { nonNullable: true, validators: [Validators.required, Validators.min(1), Validators.max(1000)] })
  });
  readonly supplierForm = new FormGroup({
    tradingName: new FormControl('', { nonNullable: true, validators: Validators.required }),
    companyRegistrationNumber: new FormControl('', { nonNullable: true, validators: Validators.required }),
    taxIdentifier: new FormControl<string|null>(null), contactPerson: new FormControl('', { nonNullable: true, validators: Validators.required }),
    contactEmail: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    phoneNumber: new FormControl('', { nonNullable: true, validators: Validators.required }), website: new FormControl<string|null>(null),
    address: new FormControl('', { nonNullable: true, validators: Validators.required }), city: new FormControl('', { nonNullable: true, validators: Validators.required }),
    county: new FormControl('', { nonNullable: true, validators: Validators.required }), latitude: new FormControl<number|null>(null),
    longitude: new FormControl<number|null>(null), description: new FormControl<string|null>(null)
  });

  constructor() {
    effect(() => {
      if (this.auth.session()) this.loadProfile();
    });
  }
  loadProfile(): void {
    const session = this.auth.session(); if (!session) return;
    this.message.set(''); this.loading.set(true); this.loadFailed.set(false);
    if (session.accountType === 'Buyer') this.profiles.getBuyer(session.userId).subscribe({ next: value => { this.loading.set(false); this.buyerForm.patchValue(value); }, error: error => this.loadError(error) });
    else this.profiles.getSupplier(session.userId).subscribe({ next: value => { this.loading.set(false); this.supplierForm.patchValue(value); }, error: error => this.loadError(error) });
  }
  saveBuyer(): void { const session = this.auth.session(); if (!session || this.buyerForm.invalid) return; this.save(this.profiles.saveBuyer(session.userId, this.buyerForm.getRawValue())); }
  saveSupplier(): void { const session = this.auth.session(); if (!session || this.supplierForm.invalid) return; this.save(this.profiles.saveSupplier(session.userId, this.supplierForm.getRawValue())); }
  private save(request: Observable<unknown>): void {
    this.saving.set(true); this.message.set('');
    request.subscribe({ next: () => { this.saving.set(false); this.hasError.set(false); this.message.set('Profilul a fost salvat.'); const accountType = this.auth.session()?.accountType; if (accountType) this.profileSaved.emit(accountType); }, error: () => { this.saving.set(false); this.hasError.set(true); this.message.set('Profilul nu a putut fi salvat. Verificați câmpurile și conexiunea cu API-ul.'); } });
  }
  private loadError(error: HttpErrorResponse): void {
    this.loading.set(false);
    if (error.status === 404) { this.hasError.set(false); this.message.set('Profilul nu este completat încă. Adaugă datele de mai jos pentru a continua.'); }
    else { this.loadFailed.set(true); this.hasError.set(true); this.message.set('Profilul nu a putut fi încărcat.'); }
  }
}
