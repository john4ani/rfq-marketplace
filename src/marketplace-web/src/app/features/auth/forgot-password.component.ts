import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-forgot-password',
  imports: [ReactiveFormsModule, RouterLink],
  template: `<section class="auth-card">
    <h1>Resetați parola</h1>
    <p>Introduceți adresa contului. Dacă există, vă trimitem un link securizat pentru alegerea unei parole noi.</p>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <label>E-mail
        <input type="email" formControlName="email" autocomplete="email">
        @if (form.controls.email.touched && form.controls.email.invalid) {
          <small class="error">Introduceți o adresă de e-mail validă.</small>
        }
      </label>
      @if (message()) { <p class="success" role="status">{{ message() }}</p> }
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      <button type="submit" [disabled]="form.invalid || submitting()">
        {{ submitting() ? 'Se trimite…' : 'Trimite linkul de resetare' }}
      </button>
    </form>
    @if (developmentToken()) {
      <details class="development-note">
        <summary>Opțiune doar pentru dezvoltare</summary>
        <p role="status">
        {{ emailSent() ? 'E-mailul de test a fost trimis. Verifică inboxul.' : (emailTransportEnabled() ? 'E-mailul de test nu a putut fi trimis. Verifică jurnalul API-ului.' : 'Trimiterea de e-mail nu este activată în configurația acestui API local.') }}
        <a routerLink="/reset-password" [queryParams]="{ email: form.controls.email.value, token: developmentToken() }">
          Continuați cu tokenul de dezvoltare
        </a>
        </p>
      </details>
    }
    <a routerLink="/sign-in">Înapoi la autentificare</a>
  </section>`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ForgotPasswordComponent {
  private readonly auth = inject(AuthService);
  readonly submitting = signal(false);
  readonly message = signal('');
  readonly error = signal('');
  readonly developmentToken = signal<string | null>(null);
  readonly emailTransportEnabled = signal(false);
  readonly emailSent = signal(false);
  readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email]
    })
  });

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.submitting.set(true);
    this.message.set('');
    this.error.set('');
    this.developmentToken.set(null);
    this.emailTransportEnabled.set(false);
    this.emailSent.set(false);
    this.auth.requestPasswordReset(this.form.controls.email.value.trim())
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: response => {
          this.message.set('Dacă adresa aparține unui cont, veți primi în scurt timp instrucțiunile de resetare.');
          this.developmentToken.set(response?.token ?? null);
          this.emailTransportEnabled.set(response?.emailTransportEnabled === true);
          this.emailSent.set(response?.emailSent === true);
        },
        error: (response: HttpErrorResponse) => this.error.set(
          response.status === 429
            ? 'Au fost prea multe solicitări. Așteptați un minut și încercați din nou.'
            : 'Solicitarea nu a putut fi trimisă. Verificați conexiunea și încercați din nou.')
      });
  }
}
