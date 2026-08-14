import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { ProfileService } from '../../core/profiles/profile.service';
import { ROMANIAN_COUNTIES } from '../../shared/romanian-counties';

@Component({
  selector: 'app-profile-editor', imports: [ReactiveFormsModule],
  template: `<section class="profile-card" aria-labelledby="personal-details-title"><div class="section-heading"><div><p class="eyebrow">Date personale</p><h2 id="personal-details-title">Despre tine</h2></div><p>Aceste informații sunt ale contului tău. Nu sunt datele unei firme.</p></div>
  @if (loading()) { <p role="status">Se încarcă datele personale…</p> }
  @else if (loadFailed()) { <div class="profile-load-error" role="alert"><p>Datele personale nu au putut fi încărcate acum.</p><button type="button" (click)="loadProfile()">Încearcă din nou</button></div> }
  @else { <form [formGroup]="form" (ngSubmit)="save()"><fieldset><legend>Contact</legend>
      <label>Nume afișat *<input formControlName="displayName" placeholder="Cum să te afișăm?"></label>
      <label>E-mail<input [value]="auth.identity()?.email ?? ''" readonly aria-readonly="true"><small>Adresa de e-mail se gestionează prin securitatea contului.</small></label>
      <label>Telefon *<input formControlName="phoneNumber" placeholder="+40 7xx xxx xxx"></label></fieldset>
      <fieldset><legend>Preferințe pentru cereri</legend><div class="form-row"><label>Oraș *<input formControlName="defaultCity"></label>
      <label>Județ *<select formControlName="defaultCounty"><option value="">Alege județul</option>@for (county of counties; track county) { <option [value]="county">{{ county }}</option> }</select></label></div>
      <label>Rază de interes (km) *<input type="number" min="1" max="1000" formControlName="marketRadiusKm"><small>O folosim pentru a căuta furnizori relevanți pentru cererile tale.</small></label></fieldset>
      <div class="form-actions"><button [disabled]="form.invalid || saving()">{{ saving() ? 'Se salvează…' : 'Salvează datele' }}</button></div></form> }
  @if (message()) { <p [class.error]="hasError()" role="status">{{ message() }}</p> }
  </section>`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProfileEditorComponent {
  readonly auth = inject(AuthService); private readonly profiles = inject(ProfileService);
  readonly counties = ROMANIAN_COUNTIES; readonly loading = signal(false); readonly saving = signal(false); readonly message = signal(''); readonly hasError = signal(false); readonly loadFailed = signal(false);
  readonly form = new FormGroup({
    displayName: new FormControl('', { nonNullable: true, validators: Validators.required }),
    buyerType: new FormControl<'Individual' | 'Company'>('Individual', { nonNullable: true }),
    companyName: new FormControl<string | null>(null), taxIdentifier: new FormControl<string | null>(null),
    phoneNumber: new FormControl('', { nonNullable: true, validators: Validators.required }),
    defaultCity: new FormControl('', { nonNullable: true, validators: Validators.required }),
    defaultCounty: new FormControl('', { nonNullable: true, validators: Validators.required }),
    marketRadiusKm: new FormControl(30, { nonNullable: true, validators: [Validators.required, Validators.min(1), Validators.max(1000)] })
  });

  constructor() { effect(() => { if (this.auth.identity()) this.loadProfile(); }); }
  loadProfile(): void {
    const identity = this.auth.identity(); if (!identity) return;
    this.message.set(''); this.loading.set(true); this.loadFailed.set(false);
    this.profiles.getBuyer(identity.userId).subscribe({ next: value => { this.form.patchValue(value); this.loading.set(false); }, error: error => this.loadError(error) });
  }
  save(): void {
    const identity = this.auth.identity(); if (!identity || this.form.invalid) return;
    this.saving.set(true); this.message.set('');
    this.profiles.saveBuyer(identity.userId, this.form.getRawValue()).subscribe({
      next: () => { this.saving.set(false); this.hasError.set(false); this.message.set('Datele personale au fost salvate.'); },
      error: () => { this.saving.set(false); this.hasError.set(true); this.message.set('Datele personale nu au putut fi salvate. Verifică informațiile și încearcă din nou.'); }
    });
  }
  private loadError(error: HttpErrorResponse): void {
    this.loading.set(false);
    if (error.status === 404) { this.hasError.set(false); this.message.set('Completează datele de mai jos când ești pregătit.'); }
    else { this.loadFailed.set(true); this.hasError.set(true); this.message.set('Datele personale nu au putut fi încărcate.'); }
  }
}
