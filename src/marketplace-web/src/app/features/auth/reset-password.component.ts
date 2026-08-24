import { HttpErrorResponse } from '@angular/common/http';
import { Location } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';

@Component({
  selector: 'app-reset-password',
  imports: [ReactiveFormsModule, RouterLink],
  template: `<section class="auth-card">
    <h1>{{ i18n.t('Alegeți o parolă nouă') }}</h1>
    @if (!hasResetLink()) {
      <p class="error" role="alert">{{ i18n.t('Linkul de resetare este incomplet. Solicitați un link nou.') }}</p>
      <a routerLink="/forgot-password">{{ i18n.t('Solicitați alt link') }}</a>
    } @else if (completed()) {
      <p class="success" role="status">{{ i18n.t('Parola a fost schimbată. Vă puteți autentifica folosind parola nouă.') }}</p>
      <a routerLink="/sign-in">{{ i18n.t('Mergeți la autentificare') }}</a>
    } @else {
      <p>{{ i18n.t('Cont:') }} <strong>{{ email }}</strong></p>
      <form [formGroup]="form" (ngSubmit)="submit()">
        <label>{{ i18n.t('Parolă nouă') }}
          <input [type]="showPassword() ? 'text' : 'password'" formControlName="password" autocomplete="new-password">
          <small>{{ i18n.t('Folosiți cel puțin 8 caractere.') }}</small>
        </label>
        <label>{{ i18n.t('Confirmați parola') }}
          <input [type]="showPassword() ? 'text' : 'password'" formControlName="confirmation" autocomplete="new-password">
        </label>
        <label class="checkbox-label">
          <input type="checkbox" [checked]="showPassword()" (change)="showPassword.set(!showPassword())">
          {{ i18n.t('Arată parolele') }}
        </label>
        @if (validationMessage()) { <p class="error" role="alert">{{ validationMessage() }}</p> }
        @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        <button type="submit" [disabled]="form.invalid || submitting()">
          {{ i18n.t(submitting() ? 'Se salvează…' : 'Salvează parola nouă') }}
        </button>
      </form>
      <a routerLink="/forgot-password">{{ i18n.t('Solicitați alt link') }}</a>
    }
  </section>`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ResetPasswordComponent {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly location = inject(Location);
  readonly i18n = inject(LanguageService);
  readonly email = this.route.snapshot.queryParamMap.get('email')?.trim() ?? '';
  private readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';
  readonly hasResetLink = signal(this.email.length > 0 && this.token.length > 0);
  readonly submitting = signal(false);
  readonly completed = signal(false);
  readonly showPassword = signal(false);
  readonly error = signal('');
  readonly validationMessage = signal('');
  readonly form = new FormGroup({
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(8)]
    }),
    confirmation: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required]
    })
  });

  submit(): void {
    this.form.markAllAsTouched();
    this.validationMessage.set('');
    this.error.set('');
    if (this.form.invalid) {
      this.validationMessage.set(this.i18n.t('Parola trebuie să conțină cel puțin 8 caractere.'));
      return;
    }
    if (this.form.controls.password.value !== this.form.controls.confirmation.value) {
      this.validationMessage.set(this.i18n.t('Parolele introduse nu coincid.'));
      return;
    }

    this.submitting.set(true);
    this.auth.resetPassword(this.email, this.token, this.form.controls.password.value)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          this.location.replaceState('/reset-password');
          this.completed.set(true);
        },
        error: (response: HttpErrorResponse) => this.error.set(
          response.status === 400
            ? this.i18n.t('Linkul este invalid sau a expirat. Solicitați un link nou.')
            : this.i18n.t('Parola nu a putut fi schimbată. Verificați conexiunea și încercați din nou.'))
      });
  }
}
