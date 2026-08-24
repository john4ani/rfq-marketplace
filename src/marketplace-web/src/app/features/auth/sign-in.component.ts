import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';

@Component({
  selector: 'app-sign-in', imports: [ReactiveFormsModule, RouterLink],
  template: `<section class="auth-card"><h1>{{ i18n.t('Autentificare') }}</h1><form [formGroup]="form" (ngSubmit)="submit()">
    <label>{{ i18n.t('E-mail') }}<input type="email" formControlName="email" autocomplete="email"><small>{{ i18n.t('Introduceți e-mailul folosit la înregistrare.') }}</small></label>
    <label>{{ i18n.t('Parolă') }}<input [type]="showPassword() ? 'text' : 'password'" formControlName="password" autocomplete="current-password"><small>{{ i18n.t('Parola conține cel puțin 8 caractere.') }}</small></label>
    <label class="checkbox-label"><input type="checkbox" [checked]="showPassword()" (change)="showPassword.set(!showPassword())">{{ i18n.t('Arată parola') }}</label>
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    <button type="submit" [disabled]="form.invalid || submitting()">{{ i18n.t('Autentificare') }}</button>
  </form><p><a routerLink="/forgot-password">{{ i18n.t('Ați uitat parola?') }}</a></p>
  <p>{{ i18n.t('Nu aveți cont?') }} <a routerLink="/register">{{ i18n.t('Creează un cont') }}</a></p></section>`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SignInComponent {
  private readonly auth = inject(AuthService); private readonly router = inject(Router); private readonly route=inject(ActivatedRoute);
  readonly i18n = inject(LanguageService);
  private readonly returnUrl=this.safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
  readonly submitting = signal(false); readonly error = signal(''); readonly showPassword = signal(false);
  readonly form = new FormGroup({ email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }), password: new FormControl('', { nonNullable: true, validators: Validators.required }) });
  submit(): void {
    if (this.form.invalid) { this.error.set(this.i18n.t('Introduceți un e-mail valid și parola.')); return; }
    this.submitting.set(true); this.error.set('');
    this.auth.login(this.form.controls.email.value, this.form.controls.password.value).subscribe({
      next: () => void this.router.navigateByUrl(this.returnUrl??'/dashboard'),
      error: (response) => {
        this.error.set(response.status === 0
          ? this.i18n.t('Nu ne putem conecta momentan. Verifică conexiunea și încearcă din nou.')
          : this.i18n.t('E-mailul sau parola este incorectă.'));
        this.submitting.set(false);
      }
    });
  }
  private safeReturnUrl(value:string|null):string|null{return value?.startsWith('/')&&!value.startsWith('//')?value:null;}
}
